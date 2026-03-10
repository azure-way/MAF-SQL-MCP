namespace MafDb.Core.Memory.Models;

public sealed class ConversationAskResult
{
    public string Answer { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public ConversationDiagnostics Diagnostics { get; init; } = new();
}
