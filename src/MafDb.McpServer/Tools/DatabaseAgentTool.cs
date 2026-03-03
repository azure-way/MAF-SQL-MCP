using System.ComponentModel;
using System.Text.Json;
using MafDb.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MafDb.McpServer.Tools;

[McpServerToolType]
public static class DatabaseAgentTool
{
    [McpServerTool, Description("Ask a natural-language question about the AdventureWorks SQL Server database. The agent will examine the schema, generate and run SQL queries, and return a natural-language answer. Pass a sessionId to continue a previous conversation, or omit it to start a new one.")]
    public static async Task<CallToolResult> AskDatabaseAgent(
        DatabaseAgent agent,
        [Description("The question to ask about the database")] string question,
        [Description("Optional session ID to continue a previous conversation")] string? sessionId = null,
        CancellationToken ct = default)
    {
        var (answer, sid) = await agent.AskAsync(question, sessionId, ct);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = answer }],
            StructuredContent = JsonSerializer.SerializeToElement(new
            {
                sessionId = sid,
                answer
            })
        };
    }
}
