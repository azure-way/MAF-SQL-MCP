using System.Text.Json;

namespace MafDb.Core.Persistence;

public interface ISessionStore
{
    Task SaveAsync(string sessionId, JsonElement sessionState, CancellationToken ct = default);
    Task<JsonElement?> LoadAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}
