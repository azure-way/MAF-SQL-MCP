using MafDb.Core;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

_ = config["SqlServer:PersistenceConnectionString"]
    ?? config["SqlServer:ConnectionString"]
    ?? throw new InvalidOperationException("Missing SqlServer:ConnectionString configuration.");

var databaseAgent = new DatabaseAgent(config);

Console.WriteLine("=== SQL Database Agent (AdventureWorks) ===");
Console.WriteLine("Enter session ID to resume, or press Enter for a new session:");
Console.Write("> ");
var sessionIdInput = Console.ReadLine()?.Trim();

var sessionId = string.IsNullOrEmpty(sessionIdInput)
    ? Guid.NewGuid().ToString("N")
    : sessionIdInput;

Console.WriteLine(string.IsNullOrEmpty(sessionIdInput)
    ? $"New session: {sessionId}"
    : $"Using session: {sessionId}");

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
        var result = await databaseAgent.AskWithDiagnosticsAsync(userInput, sessionId);
        var text = result.Answer;
        sessionId = result.SessionId;
        Console.WriteLine();
        Console.WriteLine($"Agent> {text}");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
