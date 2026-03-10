using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace MafDb.Core.Memory.Workflow;

public sealed class LlmSqlPlanner : ISqlPlanner
{
    private readonly ChatClientAgent _plannerAgent;

    public LlmSqlPlanner(ChatClientAgent plannerAgent)
    {
        _plannerAgent = plannerAgent;
    }

    public async Task<SqlPlanResponse> GeneratePlanAsync(SqlPlannerRequest request, CancellationToken ct = default)
    {
        var session = await _plannerAgent.CreateSessionAsync();
        var prompt = BuildPlannerPrompt(request);
        var response = await _plannerAgent.RunAsync(prompt, session, null, ct);

        return ParsePlan(response.Text);
    }

    public async Task<string> ComposeAnswerAsync(string question, string sql, string sqlResult, CancellationToken ct = default)
    {
        var session = await _plannerAgent.CreateSessionAsync();

        var prompt = $"""
            You are a SQL analyst assistant.
            Explain the query result in concise natural language.
            Do not include SQL unless explicitly asked.

            Question:
            {question}

            SQL used:
            {sql}

            SQL result table text:
            {sqlResult}
            """;

        var response = await _plannerAgent.RunAsync(prompt, session, null, ct);
        return response.Text ?? string.Empty;
    }

    private static string BuildPlannerPrompt(SqlPlannerRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You generate SQL for SQL Server AdventureWorks.");
        sb.AppendLine("Return JSON only.");
        sb.AppendLine("Allowed query type: SELECT only.");
        sb.AppendLine("Use schema-qualified table names.");
        sb.AppendLine();
        sb.AppendLine("Output JSON schema:");
        sb.AppendLine("{\"intent\":\"...\",\"sql\":\"...\",\"reasoning_summary\":\"...\",\"expected_columns\":[\"...\"]}");
        sb.AppendLine();
        sb.AppendLine("Intent:");
        sb.AppendLine(request.Intent);
        sb.AppendLine();
        sb.AppendLine("Selected context:");
        sb.AppendLine(request.SelectedContext);
        sb.AppendLine();
        sb.AppendLine("Database schema:");
        sb.AppendLine(request.DatabaseSchema);
        sb.AppendLine();
        sb.AppendLine("Question:");
        sb.AppendLine(request.UserQuestion);

        if (!string.IsNullOrWhiteSpace(request.PreviousSql) || !string.IsNullOrWhiteSpace(request.PreviousError))
        {
            sb.AppendLine();
            sb.AppendLine("Previous failed SQL:");
            sb.AppendLine(request.PreviousSql ?? "");
            sb.AppendLine();
            sb.AppendLine("Previous error:");
            sb.AppendLine(request.PreviousError ?? "");
            sb.AppendLine("Fix the SQL and return corrected JSON.");
        }

        return sb.ToString();
    }

    private static SqlPlanResponse ParsePlan(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            throw new InvalidOperationException("Planner returned empty response.");

        var payload = responseText.Trim();
        if (payload.StartsWith("```", StringComparison.Ordinal))
            payload = StripCodeFence(payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        return new SqlPlanResponse
        {
            Intent = root.TryGetProperty("intent", out var intent) ? intent.GetString() ?? string.Empty : string.Empty,
            Sql = root.TryGetProperty("sql", out var sql) ? sql.GetString() ?? string.Empty : string.Empty,
            ReasoningSummary = root.TryGetProperty("reasoning_summary", out var reasoning) ? reasoning.GetString() ?? string.Empty : string.Empty,
            ExpectedColumns = root.TryGetProperty("expected_columns", out var expected) && expected.ValueKind == JsonValueKind.Array
                ? expected.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                : []
        };
    }

    private static string StripCodeFence(string value)
    {
        var lines = value.Split('\n');
        if (lines.Length <= 2)
            return value;

        var body = lines.Skip(1).Take(lines.Length - 2);
        return string.Join('\n', body).Trim();
    }
}
