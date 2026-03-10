using System.Text;
using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;

namespace MafDb.Core.Memory.Selection;

public sealed class ContextSelector : IContextSelector
{
    private readonly MemoryOptions _options;

    public ContextSelector(MemoryOptions options)
    {
        _options = options;
    }

    public ContextSelectionResult Select(ConversationState state, string question)
    {
        var minRecentWindow = 4;
        var recentWindowSize = Math.Max(minRecentWindow, _options.RecentWindowSize);

        var recentTurns = state.Turns.TakeLast(recentWindowSize).ToList();
        var olderTurns = state.Turns.Take(Math.Max(0, state.Turns.Count - recentTurns.Count)).ToList();

        var retrievedTurns = olderTurns
            .Select(turn => new { Turn = turn, Score = LexicalScore(turn.Text, question) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Turn.TimestampUtc)
            .Take(_options.MaxRetrievedTurns)
            .Select(x => x.Turn)
            .OrderBy(t => t.TimestampUtc)
            .ToList();

        var selectedFacts = state.PinnedFacts
            .OrderByDescending(f => f.LastUsedUtc)
            .ThenByDescending(f => f.Confidence)
            .Take(_options.MaxPinnedFacts)
            .ToList();

        var summary = TruncateToTokens(state.RollingSummary, _options.SummaryTokenBudget);
        var recent = recentTurns;
        var retrieved = retrievedTurns;

        var context = BuildSelectedContext(summary, selectedFacts, recent, retrieved);
        var tokens = EstimateTokens(context) + EstimateTokens(question);

        while (tokens > _options.ContextTokenBudget && retrieved.Count > 0)
        {
            retrieved.RemoveAt(0);
            context = BuildSelectedContext(summary, selectedFacts, recent, retrieved);
            tokens = EstimateTokens(context) + EstimateTokens(question);
        }

        if (tokens > _options.ContextTokenBudget)
        {
            summary = TruncateToTokens(summary, Math.Max(120, _options.SummaryTokenBudget / 2));
            context = BuildSelectedContext(summary, selectedFacts, recent, retrieved);
            tokens = EstimateTokens(context) + EstimateTokens(question);
        }

        while (tokens > _options.ContextTokenBudget && recent.Count > minRecentWindow)
        {
            recent.RemoveAt(0);
            context = BuildSelectedContext(summary, selectedFacts, recent, retrieved);
            tokens = EstimateTokens(context) + EstimateTokens(question);
        }

        foreach (var fact in selectedFacts)
            fact.LastUsedUtc = DateTimeOffset.UtcNow;

        return new ContextSelectionResult
        {
            SelectedContext = context,
            SelectedRecentTurns = recent.Count,
            SelectedRetrievedTurns = retrieved.Count,
            SelectedPinnedFacts = selectedFacts.Count,
            EstimatedTokens = tokens,
            RecentTurns = recent,
            RetrievedTurns = retrieved,
            PinnedFacts = selectedFacts
        };
    }

    private static string BuildSelectedContext(
        string summary,
        IReadOnlyCollection<PinnedFact> facts,
        IReadOnlyCollection<ConversationTurn> recent,
        IReadOnlyCollection<ConversationTurn> retrieved)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine("[Rolling Summary]");
            sb.AppendLine(summary);
            sb.AppendLine();
        }

        if (facts.Count > 0)
        {
            sb.AppendLine("[Pinned Facts]");
            foreach (var fact in facts)
                sb.AppendLine("- " + fact.Fact);
            sb.AppendLine();
        }

        if (retrieved.Count > 0)
        {
            sb.AppendLine("[Relevant Older Turns]");
            foreach (var turn in retrieved)
                sb.AppendLine($"- {turn.Role}: {turn.Text}");
            sb.AppendLine();
        }

        if (recent.Count > 0)
        {
            sb.AppendLine("[Recent Turns]");
            foreach (var turn in recent)
                sb.AppendLine($"- {turn.Role}: {turn.Text}");
        }

        return sb.ToString();
    }

    private static double LexicalScore(string existingText, string currentQuestion)
    {
        var a = Tokenize(existingText);
        var b = Tokenize(currentQuestion);

        if (a.Count == 0 || b.Count == 0)
            return 0;

        var overlap = a.Intersect(b).Count();
        return (double)overlap / b.Count;
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '-', '_', '/', '\\', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return Math.Max(1, text.Length / 4);
    }

    private static string TruncateToTokens(string text, int maxTokens)
    {
        var maxChars = Math.Max(1, maxTokens * 4);
        return Truncate(text, maxChars);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;

        return text[..maxChars] + "...";
    }
}
