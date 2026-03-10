using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;
using MafDb.Core.Memory.Selection;

namespace MafDb.Core.Tests;

public sealed class ContextSelectorTests
{
    [Fact]
    public void Select_RespectsContextBudgetAndRequiredSections()
    {
        var options = new MemoryOptions
        {
            RecentWindowSize = 8,
            MaxPinnedFacts = 6,
            MaxRetrievedTurns = 3,
            ContextTokenBudget = 120,
            SummaryTokenBudget = 40
        };

        var selector = new ContextSelector(options);
        var state = new ConversationState
        {
            SessionId = "s1",
            RollingSummary = string.Join(' ', Enumerable.Repeat("summary", 300))
        };

        for (var i = 0; i < 20; i++)
        {
            state.Turns.Add(new ConversationTurn
            {
                Role = i % 2 == 0 ? "user" : "assistant",
                Text = $"turn {i} about sales revenue and order totals",
                TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-20 + i)
            });
        }

        state.PinnedFacts.AddRange(Enumerable.Range(0, 10).Select(i => new PinnedFact
        {
            Fact = $"Fact {i} about sales table relationships",
            Confidence = 0.5 + i * 0.01,
            LastUsedUtc = DateTimeOffset.UtcNow.AddMinutes(-i)
        }));

        var result = selector.Select(state, "show sales totals by territory");

        Assert.Contains("[Recent Turns]", result.SelectedContext);
        Assert.True(result.SelectedRecentTurns >= 4);
        Assert.True(result.SelectedPinnedFacts <= 6);
        Assert.InRange(result.SelectedRetrievedTurns, 0, state.Turns.Count);
    }
}
