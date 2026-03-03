using System.Collections.Concurrent;
using System.Text.Json;

namespace MafDb.Core.Persistence;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, JsonElement> _store = new();

    public Task SaveAsync(string sessionId, JsonElement sessionState, CancellationToken ct = default)
    {
        _store[sessionId] = sessionState;
        return Task.CompletedTask;
    }

    public Task<JsonElement?> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(sessionId, out var state))
            return Task.FromResult<JsonElement?>(state);

        return Task.FromResult<JsonElement?>(null);
    }

    public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        return Task.FromResult(_store.ContainsKey(sessionId));
    }
}
