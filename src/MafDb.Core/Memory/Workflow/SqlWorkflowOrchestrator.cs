using System.Collections.Concurrent;
using System.Text.Json;
using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;
using MafDb.Core.Memory.Persistence;
using MafDb.Core.Memory.Selection;

namespace MafDb.Core.Memory.Workflow;

public sealed class SqlWorkflowOrchestrator : ISqlWorkflowOrchestrator
{
    private readonly IConversationStateStore _stateStore;
    private readonly IContextSelector _contextSelector;
    private readonly ISchemaCache _schemaCache;
    private readonly ISqlPlanner _planner;
    private readonly ISqlValidator _validator;
    private readonly IQueryExecutor _executor;
    private readonly MemoryOptions _memoryOptions;
    private readonly WorkflowOptions _workflowOptions;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new();

    public SqlWorkflowOrchestrator(
        IConversationStateStore stateStore,
        IContextSelector contextSelector,
        ISchemaCache schemaCache,
        ISqlPlanner planner,
        ISqlValidator validator,
        IQueryExecutor executor,
        MemoryOptions memoryOptions,
        WorkflowOptions workflowOptions)
    {
        _stateStore = stateStore;
        _contextSelector = contextSelector;
        _schemaCache = schemaCache;
        _planner = planner;
        _validator = validator;
        _executor = executor;
        _memoryOptions = memoryOptions;
        _workflowOptions = workflowOptions;
    }

    public async Task<WorkflowAskResult> AskAsync(string question, string? sessionId = null, CancellationToken ct = default)
    {
        sessionId ??= Guid.NewGuid().ToString("N");
        var gate = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct);
        try
        {
            var state = await _stateStore.LoadConversationStateAsync(sessionId, ct) ?? await InitializeStateFromLegacyAsync(sessionId, ct);

            PersistTurn(state, "user", question, "incoming", null);

            var intent = ClassifyIntent(question);
            ExtractFacts(state, question, state.Turns.Last().TurnId, DateTimeOffset.UtcNow);

            var schema = await _schemaCache.LoadOrRefreshAsync(state, ct);
            var selection = _contextSelector.Select(state, question);

            var execution = await RunPlanningAndExecutionAsync(question, intent, schema, selection.SelectedContext, ct);

            var answer = await _planner.ComposeAnswerAsync(question, execution.FinalSql, execution.QueryResult, ct);
            if (_workflowOptions.ReturnSqlInUserText)
                answer = answer + Environment.NewLine + Environment.NewLine + "SQL:" + Environment.NewLine + execution.FinalSql;

            PersistTurn(state, "assistant", answer, intent, "ExecuteSqlQuery");
            ExtractFacts(state, answer, state.Turns.Last().TurnId, DateTimeOffset.UtcNow);
            UpdateRollingSummary(state, question, answer);

            state.LastSelectedContext = selection.SelectedContext;
            state.LastSqlCandidates = execution.SqlCandidates;
            state.LastFinalSql = execution.FinalSql;
            state.LastRetryCount = execution.RetryCount;
            state.LastDatabaseErrorCategory = execution.ErrorCategory;
            state.LastDiagnosticsJson = JsonSerializer.Serialize(new
            {
                validation = execution.ValidationOutcome,
                retries = execution.RetryCount,
                sql = execution.FinalSql
            });
            state.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _stateStore.SaveConversationStateAsync(state, ct);

            return new WorkflowAskResult
            {
                Answer = answer,
                SessionId = sessionId,
                Diagnostics = new ConversationDiagnostics
                {
                    MemoryMode = _memoryOptions.Mode.ToString(),
                    WorkflowMode = _workflowOptions.Mode.ToString(),
                    Intent = intent,
                    SelectedPinnedFacts = selection.SelectedPinnedFacts,
                    SelectedRecentTurns = selection.SelectedRecentTurns,
                    SelectedRetrievedTurns = selection.SelectedRetrievedTurns,
                    EstimatedContextTokens = selection.EstimatedTokens,
                    FinalSql = execution.FinalSql,
                    RetryCount = execution.RetryCount,
                    ValidationOutcome = execution.ValidationOutcome,
                    UsedFallbackPath = false,
                    ErrorCategory = execution.ErrorCategory
                }
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ExecutionOutcome> RunPlanningAndExecutionAsync(
        string question,
        string intent,
        string schema,
        string selectedContext,
        CancellationToken ct)
    {
        var candidates = new List<string>();
        var retry = 0;
        string? lastError = null;
        string? previousSql = null;

        while (true)
        {
            var plan = await _planner.GeneratePlanAsync(new SqlPlannerRequest
            {
                UserQuestion = question,
                Intent = intent,
                DatabaseSchema = schema,
                SelectedContext = selectedContext,
                PreviousError = lastError,
                PreviousSql = previousSql
            }, ct);

            candidates.Add(plan.Sql);

            var validation = _validator.Validate(plan.Sql);
            if (!validation.IsValid)
            {
                if (_workflowOptions.FailClosedOnValidation)
                    throw new InvalidOperationException("SQL validation failed: " + validation.Error);

                lastError = validation.Error;
                previousSql = plan.Sql;
                retry++;
                if (retry > _workflowOptions.MaxRepairRetries)
                    throw new InvalidOperationException("Validation failed and max retries reached.");
                continue;
            }

            try
            {
                var result = await _executor.ExecuteAsync(validation.NormalizedSql, ct);
                return new ExecutionOutcome(
                    FinalSql: validation.NormalizedSql,
                    QueryResult: result,
                    RetryCount: retry,
                    SqlCandidates: candidates,
                    ValidationOutcome: "valid",
                    ErrorCategory: lastError is null ? null : "sql_repaired");
            }
            catch (Exception ex)
            {
                lastError = ClassifyDbError(ex.Message);
                previousSql = validation.NormalizedSql;
                retry++;

                if (retry > _workflowOptions.MaxRepairRetries)
                    throw new InvalidOperationException("SQL execution failed after retries: " + ex.Message, ex);
            }
        }
    }

    private async Task<ConversationState> InitializeStateFromLegacyAsync(string sessionId, CancellationToken ct)
    {
        var state = new ConversationState
        {
            SessionId = sessionId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var legacy = await _stateStore.LoadLegacySessionStateAsync(sessionId, ct);
        if (legacy is not null)
        {
            state.RollingSummary = "Legacy full-history session detected before deterministic workflow mode.";
            state.PinnedFacts.Add(new PinnedFact
            {
                FactId = "legacy-migration",
                Fact = "Conversation migrated from legacy full-history mode.",
                SourceTurnId = "legacy",
                Confidence = 0.6,
                LastUsedUtc = DateTimeOffset.UtcNow
            });
        }

        return state;
    }

    private static string ClassifyIntent(string question)
    {
        var q = question.ToLowerInvariant();

        if (q.Contains("schema") || q.Contains("table") || q.Contains("column") || q.Contains("relationship"))
            return "schema_exploration";
        if (q.Contains("where") || q.Contains("join") || q.Contains("filter") || q.Contains("group by"))
            return "query_refinement";
        if (q.Contains("why") || q.Contains("explain") || q.Contains("clarify"))
            return "clarification";

        return "follow_up";
    }

    private static string ClassifyDbError(string message)
    {
        var m = message.ToLowerInvariant();
        if (m.Contains("invalid object name") || m.Contains("invalid column name"))
            return "schema_mismatch";
        if (m.Contains("syntax"))
            return "syntax_error";
        if (m.Contains("timeout"))
            return "timeout";

        return "execution_error";
    }

    private static void PersistTurn(ConversationState state, string role, string text, string intent, string? toolName)
    {
        state.Turns.Add(new ConversationTurn
        {
            TurnId = Guid.NewGuid().ToString("N"),
            Role = role,
            Text = text,
            TokenEstimate = EstimateTokens(text),
            TimestampUtc = DateTimeOffset.UtcNow,
            SemanticTags = [intent],
            ToolName = toolName
        });
    }

    private static void UpdateRollingSummary(ConversationState state, string question, string answer)
    {
        var delta = $"User asked: {Truncate(question, 280)} Assistant answered: {Truncate(answer, 280)}";
        state.RollingSummary = string.IsNullOrWhiteSpace(state.RollingSummary)
            ? delta
            : state.RollingSummary + Environment.NewLine + delta;
    }

    private static void ExtractFacts(ConversationState state, string text, string sourceTurnId, DateTimeOffset now)
    {
        var candidates = text
            .Split(['.', '\n', ';', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length >= 20 && line.Length <= 180)
            .Where(line => line.Contains(' ') && (line.Contains("is ", StringComparison.OrdinalIgnoreCase)
                                                || line.Contains("has ", StringComparison.OrdinalIgnoreCase)
                                                || line.Contains("contains ", StringComparison.OrdinalIgnoreCase)
                                                || line.Contains("table", StringComparison.OrdinalIgnoreCase)
                                                || line.Contains("column", StringComparison.OrdinalIgnoreCase)
                                                || line.Contains("sales", StringComparison.OrdinalIgnoreCase)))
            .Take(5)
            .ToList();

        foreach (var candidate in candidates)
        {
            var normalized = candidate.Trim();
            var existing = state.PinnedFacts.FirstOrDefault(f => string.Equals(f.Fact, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Confidence = Math.Min(1.0, existing.Confidence + 0.05);
                existing.LastUsedUtc = now;
                existing.SourceTurnId = sourceTurnId;
                continue;
            }

            state.PinnedFacts.Add(new PinnedFact
            {
                FactId = Guid.NewGuid().ToString("N"),
                Fact = normalized,
                SourceTurnId = sourceTurnId,
                Confidence = 0.55,
                LastUsedUtc = now
            });
        }

        if (state.PinnedFacts.Count > 64)
        {
            state.PinnedFacts = state.PinnedFacts
                .OrderByDescending(f => f.Confidence)
                .ThenByDescending(f => f.LastUsedUtc)
                .Take(64)
                .ToList();
        }
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return Math.Max(1, text.Length / 4);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;

        return text[..maxChars] + "...";
    }

    private sealed record ExecutionOutcome(
        string FinalSql,
        string QueryResult,
        int RetryCount,
        List<string> SqlCandidates,
        string ValidationOutcome,
        string? ErrorCategory);
}
