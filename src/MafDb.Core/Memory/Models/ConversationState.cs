namespace MafDb.Core.Memory.Models;

public sealed class ConversationState
{
    public string SessionId { get; set; } = string.Empty;
    public string RollingSummary { get; set; } = string.Empty;
    public List<PinnedFact> PinnedFacts { get; set; } = [];
    public List<ConversationTurn> Turns { get; set; } = [];
    public string LastSelectedContext { get; set; } = string.Empty;
    public string? CachedSchema { get; set; }
    public string? CachedSchemaHash { get; set; }
    public DateTimeOffset? CachedSchemaAtUtc { get; set; }
    public List<string> LastSqlCandidates { get; set; } = [];
    public string? LastFinalSql { get; set; }
    public int LastRetryCount { get; set; }
    public string? LastDatabaseErrorCategory { get; set; }
    public string? LastDiagnosticsJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
