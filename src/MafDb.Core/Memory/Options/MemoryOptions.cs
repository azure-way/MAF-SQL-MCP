namespace MafDb.Core.Memory.Options;

public enum MemoryMode
{
    StateGraph,
    FullHistory
}

public sealed class MemoryOptions
{
    public MemoryMode Mode { get; set; } = MemoryMode.StateGraph;
    public int RecentWindowSize { get; set; } = 8;
    public int MaxPinnedFacts { get; set; } = 6;
    public int MaxRetrievedTurns { get; set; } = 3;
    public int ContextTokenBudget { get; set; } = 2500;
    public int SummaryTokenBudget { get; set; } = 800;
    public bool EnableFallbackOnError { get; set; } = true;
}
