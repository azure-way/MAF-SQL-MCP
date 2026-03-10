namespace MafDb.Core.Memory.Models;

public sealed class ConversationDiagnostics
{
    public string MemoryMode { get; set; } = string.Empty;
    public string WorkflowMode { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public int SelectedRecentTurns { get; set; }
    public int SelectedRetrievedTurns { get; set; }
    public int SelectedPinnedFacts { get; set; }
    public int EstimatedContextTokens { get; set; }
    public string? FinalSql { get; set; }
    public int RetryCount { get; set; }
    public string ValidationOutcome { get; set; } = string.Empty;
    public bool UsedFallbackPath { get; set; }
    public string? ErrorCategory { get; set; }
}
