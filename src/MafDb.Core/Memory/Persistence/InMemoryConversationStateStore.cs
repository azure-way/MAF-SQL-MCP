using System.Collections.Concurrent;
using System.Text.Json;
using MafDb.Core.Memory.Models;

namespace MafDb.Core.Memory.Persistence;

public sealed class InMemoryConversationStateStore : IConversationStateStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _states = new();
    private readonly ConcurrentDictionary<string, JsonElement> _legacySessions = new();

    public Task<ConversationState?> LoadConversationStateAsync(string sessionId, CancellationToken ct = default)
    {
        if (_states.TryGetValue(sessionId, out var state))
            return Task.FromResult<ConversationState?>(CloneState(state));

        return Task.FromResult<ConversationState?>(null);
    }

    public Task SaveConversationStateAsync(ConversationState state, CancellationToken ct = default)
    {
        _states[state.SessionId] = CloneState(state);
        return Task.CompletedTask;
    }

    public Task<JsonElement?> LoadLegacySessionStateAsync(string sessionId, CancellationToken ct = default)
    {
        if (_legacySessions.TryGetValue(sessionId, out var state))
            return Task.FromResult<JsonElement?>(state);

        return Task.FromResult<JsonElement?>(null);
    }

    public Task SaveLegacySessionStateAsync(string sessionId, JsonElement sessionState, CancellationToken ct = default)
    {
        _legacySessions[sessionId] = sessionState;
        return Task.CompletedTask;
    }

    private static ConversationState CloneState(ConversationState state)
    {
        return new ConversationState
        {
            SessionId = state.SessionId,
            RollingSummary = state.RollingSummary,
            LastSelectedContext = state.LastSelectedContext,
            CreatedAtUtc = state.CreatedAtUtc,
            UpdatedAtUtc = state.UpdatedAtUtc,
            Turns = state.Turns
                .Select(t => new ConversationTurn
                {
                    TurnId = t.TurnId,
                    Role = t.Role,
                    Text = t.Text,
                    TimestampUtc = t.TimestampUtc,
                    TokenEstimate = t.TokenEstimate,
                    SemanticTags = t.SemanticTags.ToArray(),
                    ToolName = t.ToolName
                })
                .ToList(),
            PinnedFacts = state.PinnedFacts
                .Select(f => new PinnedFact
                {
                    FactId = f.FactId,
                    Fact = f.Fact,
                    SourceTurnId = f.SourceTurnId,
                    Confidence = f.Confidence,
                    LastUsedUtc = f.LastUsedUtc
                })
                .ToList()
        };
    }
}
