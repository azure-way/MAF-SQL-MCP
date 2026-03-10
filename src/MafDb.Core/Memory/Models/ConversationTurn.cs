namespace MafDb.Core.Memory.Models;

public sealed class ConversationTurn
{
    public string TurnId { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public int TokenEstimate { get; set; }
    public string[] SemanticTags { get; set; } = [];
    public string? ToolName { get; set; }
}
