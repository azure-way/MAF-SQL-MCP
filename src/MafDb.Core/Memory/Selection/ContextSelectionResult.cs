using MafDb.Core.Memory.Models;

namespace MafDb.Core.Memory.Selection;

public sealed class ContextSelectionResult
{
    public string SelectedContext { get; init; } = string.Empty;
    public int SelectedRecentTurns { get; init; }
    public int SelectedRetrievedTurns { get; init; }
    public int SelectedPinnedFacts { get; init; }
    public int EstimatedTokens { get; init; }
    public List<ConversationTurn> RecentTurns { get; init; } = [];
    public List<ConversationTurn> RetrievedTurns { get; init; } = [];
    public List<PinnedFact> PinnedFacts { get; init; } = [];
}
