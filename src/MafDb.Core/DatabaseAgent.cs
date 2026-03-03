using System.Collections.Concurrent;
using Azure.AI.OpenAI;
using Azure.Identity;
using MafDb.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace MafDb.Core;

public sealed class DatabaseAgent
{
    private readonly ChatClientAgent _agent;
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

    public ChatClientAgent Agent => _agent;

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

        _agent = chatClient.AsAIAgent(
            instructions: """
                You are a database assistant for the AdventureWorks SQL Server database.
                Use GetDatabaseSchema to understand the database structure, then use ExecuteSqlQuery to run queries and answer user questions.
                Always explain what you found in natural language.
                When generating SQL, use the correct schema-qualified table names (e.g., Person.Person, Sales.SalesOrderHeader).
                Only generate SELECT queries — never INSERT, UPDATE, DELETE, or DDL statements.
                """,
            name: "SqlAgent",
            description: "A conversational SQL database assistant",
            tools: tools);
    }

    public async Task<(string Answer, string SessionId)> AskAsync(
        string question, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= Guid.NewGuid().ToString("N");

        var entry = _sessions.GetOrAdd(sessionId, _ => new SessionEntry());

        await entry.Lock.WaitAsync(cancellationToken);
        try
        {
            entry.Session ??= await _agent.CreateSessionAsync();

            var response = await _agent.RunAsync(question, entry.Session, null, cancellationToken);
            return (response.Text ?? string.Empty, sessionId);
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    private sealed class SessionEntry
    {
        public AgentSession? Session;
        public readonly SemaphoreSlim Lock = new(1, 1);
    }
}
