using MafDb.Core;
using MafDb.Core.Persistence;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var persistenceConnectionString = config["SqlServer:PersistenceConnectionString"]
    ?? config["SqlServer:ConnectionString"]
    ?? throw new InvalidOperationException("Missing SqlServer:ConnectionString configuration.");

var databaseAgent = new DatabaseAgent(config);
var agent = databaseAgent.Agent;

// Session persistence (SQL-backed, same database)
ISessionStore sessionStore = new SqlSessionStore(persistenceConnectionString);

Console.WriteLine("=== SQL Database Agent (AdventureWorks) ===");
Console.WriteLine("Enter session ID to resume, or press Enter for a new session:");
Console.Write("> ");
var sessionIdInput = Console.ReadLine()?.Trim();

string sessionId;
AgentSession session;

if (!string.IsNullOrEmpty(sessionIdInput) && await sessionStore.ExistsAsync(sessionIdInput))
{
    sessionId = sessionIdInput;
    var savedState = await sessionStore.LoadAsync(sessionId);
    session = await agent.DeserializeSessionAsync(savedState!.Value);
    Console.WriteLine($"Resumed session: {sessionId}");
}
else
{
    sessionId = Guid.NewGuid().ToString("N");
    session = await agent.CreateSessionAsync();
    Console.WriteLine($"New session: {sessionId}");
}

Console.WriteLine();
Console.WriteLine($"Session ID: {sessionId}");
Console.WriteLine();
Console.WriteLine("Type your question (or 'quit' to exit):");

while (true)
{
    Console.Write("You> ");
    var userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput))
        continue;

    if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Session saved. To resume, use session ID: {sessionId}");
        break;
    }

    try
    {
        var response = await agent.RunAsync(userInput, session);
        var text = response.Text;
        Console.WriteLine();
        Console.WriteLine($"Agent> {text}");
        Console.WriteLine();

        // Persist session after each turn
        var serialized = await agent.SerializeSessionAsync(session);
        await sessionStore.SaveAsync(sessionId, serialized);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
