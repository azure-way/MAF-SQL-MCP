using System.Collections.Concurrent;
using Azure.AI.OpenAI;
using Azure.Identity;
using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;
using MafDb.Core.Memory.Persistence;
using MafDb.Core.Memory.Selection;
using MafDb.Core.Memory.Workflow;
using MafDb.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace MafDb.Core;

public sealed class DatabaseAgent
{
    private readonly ChatClientAgent _fullHistoryAgent;
    private readonly ISqlWorkflowOrchestrator _workflow;
    private readonly IConversationStateStore _stateStore;
    private readonly MemoryOptions _memoryOptions;
    private readonly WorkflowOptions _workflowOptions;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new();
    private readonly ConcurrentDictionary<string, AgentSession> _fullHistorySessions = new();

    public ChatClientAgent Agent => _fullHistoryAgent;

    public DatabaseAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Missing AzureOpenAI:Endpoint configuration.");
        var deploymentName = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Missing AzureOpenAI:DeploymentName configuration.");
        var connectionString = configuration["SqlServer:ConnectionString"]
            ?? throw new InvalidOperationException("Missing SqlServer:ConnectionString configuration.");

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        ChatClient chatClient = azureClient.GetChatClient(deploymentName);

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                (CancellationToken ct) => SqlServerTools.GetDatabaseSchema(connectionString, ct),
                "GetDatabaseSchema",
                "Gets the database schema including table names, column names and types, primary keys, and foreign key relationships. Call this first to understand the database structure before writing queries."),
            AIFunctionFactory.Create(
                (string sqlQuery, CancellationToken ct) => SqlServerTools.ExecuteSqlQuery(connectionString, sqlQuery, ct),
                "ExecuteSqlQuery",
                "Executes a read-only SQL query against the database and returns results as a formatted table. The query is wrapped in a transaction that is always rolled back for safety. Maximum 100 rows returned.")
        };

        _fullHistoryAgent = chatClient.AsAIAgent(
            instructions: """
                You are a database assistant for the AdventureWorks SQL Server database.
                Use GetDatabaseSchema to understand the database structure, then use ExecuteSqlQuery to run queries and answer user questions.
                Always explain what you found in natural language.
                When generating SQL, use the correct schema-qualified table names (e.g., Person.Person, Sales.SalesOrderHeader).
                Only generate SELECT queries - never INSERT, UPDATE, DELETE, or DDL statements.
                """,
            name: "SqlAgent",
            description: "A conversational SQL database assistant",
            tools: tools);

        var plannerAgent = chatClient.AsAIAgent(
            instructions: """
                You are a SQL planning assistant for SQL Server.
                Return machine-readable outputs exactly as requested.
                Never produce mutating SQL. Produce SELECT-only SQL.
                """,
            name: "SqlPlannerAgent",
            description: "Deterministic SQL planning and answer composition assistant");

        var persistenceConnectionString = configuration["SqlServer:PersistenceConnectionString"]
            ?? connectionString;

        _stateStore = new SqlConversationStateStore(persistenceConnectionString);

        _memoryOptions = LoadMemoryOptions(configuration);
        _workflowOptions = LoadWorkflowOptions(configuration);

        var selector = new ContextSelector(_memoryOptions);
        var schemaCache = new SessionSchemaCache(connectionString, _workflowOptions);
        var planner = new LlmSqlPlanner(plannerAgent);
        var validator = new SqlValidator();
        var executor = new SqlQueryExecutor(connectionString);

        _workflow = new SqlWorkflowOrchestrator(
            _stateStore,
            selector,
            schemaCache,
            planner,
            validator,
            executor,
            _memoryOptions,
            _workflowOptions);
    }

    public async Task<(string Answer, string SessionId)> AskAsync(
        string question, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var result = await AskWithDiagnosticsAsync(question, sessionId, cancellationToken);
        return (result.Answer, result.SessionId);
    }

    public async Task<ConversationAskResult> AskWithDiagnosticsAsync(
        string question, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        if (_memoryOptions.Mode == MemoryMode.FullHistory || _workflowOptions.Mode == WorkflowMode.ToolCalling)
            return await AskFullHistoryAsync(question, sessionId, cancellationToken);

        try
        {
            var result = await _workflow.AskAsync(question, sessionId, cancellationToken);
            return new ConversationAskResult
            {
                Answer = result.Answer,
                SessionId = result.SessionId,
                Diagnostics = result.Diagnostics
            };
        }
        catch when (_memoryOptions.EnableFallbackOnError)
        {
            var fallback = await AskFullHistoryAsync(question, sessionId, cancellationToken);
            fallback.Diagnostics.UsedFallbackPath = true;
            fallback.Diagnostics.WorkflowMode = WorkflowMode.ToolCalling.ToString();
            return fallback;
        }
    }

    private async Task<ConversationAskResult> AskFullHistoryAsync(
        string question,
        string? requestedSessionId,
        CancellationToken ct)
    {
        var sessionId = requestedSessionId ?? Guid.NewGuid().ToString("N");
        var gate = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct);
        try
        {
            if (!_fullHistorySessions.TryGetValue(sessionId, out var session))
            {
                var legacy = await _stateStore.LoadLegacySessionStateAsync(sessionId, ct);
                session = legacy is null
                    ? await _fullHistoryAgent.CreateSessionAsync()
                    : await _fullHistoryAgent.DeserializeSessionAsync(legacy.Value);
                _fullHistorySessions[sessionId] = session;
            }

            var response = await _fullHistoryAgent.RunAsync(question, session, null, ct);
            var answer = response.Text ?? string.Empty;

            var serialized = await _fullHistoryAgent.SerializeSessionAsync(session);
            await _stateStore.SaveLegacySessionStateAsync(sessionId, serialized, ct);

            return new ConversationAskResult
            {
                Answer = answer,
                SessionId = sessionId,
                Diagnostics = new ConversationDiagnostics
                {
                    MemoryMode = MemoryMode.FullHistory.ToString(),
                    WorkflowMode = WorkflowMode.ToolCalling.ToString(),
                    Intent = "full_history",
                    EstimatedContextTokens = Math.Max(1, question.Length / 4),
                    ValidationOutcome = "n/a"
                }
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private static MemoryOptions LoadMemoryOptions(IConfiguration configuration)
    {
        static int IntOrDefault(string? value, int fallback) =>
            int.TryParse(value, out var parsed) ? parsed : fallback;
        static bool BoolOrDefault(string? value, bool fallback) =>
            bool.TryParse(value, out var parsed) ? parsed : fallback;

        var modeValue = configuration["Memory:Mode"];
        var mode = string.Equals(modeValue, "FullHistory", StringComparison.OrdinalIgnoreCase)
            ? MemoryMode.FullHistory
            : MemoryMode.StateGraph;

        return new MemoryOptions
        {
            Mode = mode,
            RecentWindowSize = IntOrDefault(configuration["Memory:RecentWindowSize"], 8),
            MaxPinnedFacts = IntOrDefault(configuration["Memory:MaxPinnedFacts"], 6),
            MaxRetrievedTurns = IntOrDefault(configuration["Memory:MaxRetrievedTurns"], 3),
            ContextTokenBudget = IntOrDefault(configuration["Memory:ContextTokenBudget"], 2500),
            SummaryTokenBudget = IntOrDefault(configuration["Memory:SummaryTokenBudget"], 800),
            EnableFallbackOnError = BoolOrDefault(configuration["Memory:EnableFallbackOnError"], true)
        };
    }

    private static WorkflowOptions LoadWorkflowOptions(IConfiguration configuration)
    {
        static int IntOrDefault(string? value, int fallback) =>
            int.TryParse(value, out var parsed) ? parsed : fallback;
        static bool BoolOrDefault(string? value, bool fallback) =>
            bool.TryParse(value, out var parsed) ? parsed : fallback;

        var modeValue = configuration["Workflow:Mode"];
        var mode = string.Equals(modeValue, "ToolCalling", StringComparison.OrdinalIgnoreCase)
            ? WorkflowMode.ToolCalling
            : WorkflowMode.Deterministic;

        return new WorkflowOptions
        {
            Mode = mode,
            MaxRepairRetries = IntOrDefault(configuration["Workflow:MaxRepairRetries"], 3),
            SchemaCacheTtlMinutes = IntOrDefault(configuration["Workflow:SchemaCacheTtlMinutes"], 30),
            ReturnSqlInUserText = BoolOrDefault(configuration["Workflow:ReturnSqlInUserText"], false),
            FailClosedOnValidation = BoolOrDefault(configuration["Workflow:FailClosedOnValidation"], true)
        };
    }
}
