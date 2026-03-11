using MafDb.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

builder.Services.AddSingleton<DatabaseAgent>();

builder.AddAIAgent("SqlAgent", (sp, _) =>
{
    var databaseAgent = sp.GetRequiredService<DatabaseAgent>();
    var innerAgent = (AIAgent)databaseAgent.Agent;

    return innerAgent.AsBuilder().Use(
        runFunc: async (messages, session, options, _, ct) =>
        {
            var question = ExtractUserQuestion(messages);
            var sessionId = ResolveSessionId(session, options);
            var result = await databaseAgent.AskWithDiagnosticsAsync(question, sessionId, ct);

            return new AgentResponse(new ChatMessage(ChatRole.Assistant, result.Answer));
        },
        runStreamingFunc: (messages, session, options, _, ct) =>
            ExecuteStreaming(databaseAgent, messages, session, options, ct)).Build(sp);
});
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();
builder.AddDevUI();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/devui"));
app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

app.Run();

static string ExtractUserQuestion(IEnumerable<ChatMessage> messages)
{
    var lastUser = messages.LastOrDefault(m => m.Role == ChatRole.User && !string.IsNullOrWhiteSpace(m.Text));
    if (lastUser is not null)
        return lastUser.Text;

    var fallback = messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text));
    if (fallback is not null)
        return fallback.Text;

    throw new InvalidOperationException("No user message was provided.");
}

static string ResolveSessionId(AgentSession? session, AgentRunOptions? options)
{
    const string stateBagKey = "mafdb.devui.session_id";

    if (session?.StateBag.TryGetValue<string>(stateBagKey, out var existingSessionId) == true &&
        !string.IsNullOrWhiteSpace(existingSessionId))
    {
        return existingSessionId;
    }

    string? fromOptions = null;
    if (options?.AdditionalProperties is not null)
    {
        foreach (var kvp in options.AdditionalProperties)
        {
            if (kvp.Key.Contains("conversation", StringComparison.OrdinalIgnoreCase) &&
                kvp.Value is string value &&
                !string.IsNullOrWhiteSpace(value))
            {
                fromOptions = value;
                break;
            }
        }
    }

    var sessionId = fromOptions ?? Guid.NewGuid().ToString("N");
    session?.StateBag.SetValue(stateBagKey, sessionId);
    return sessionId;
}

static async IAsyncEnumerable<AgentResponseUpdate> ExecuteStreaming(
    DatabaseAgent databaseAgent,
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
{
    var question = ExtractUserQuestion(messages);
    var sessionId = ResolveSessionId(session, options);
    var result = await databaseAgent.AskWithDiagnosticsAsync(question, sessionId, cancellationToken);
    yield return new AgentResponseUpdate(ChatRole.Assistant, result.Answer);
}
