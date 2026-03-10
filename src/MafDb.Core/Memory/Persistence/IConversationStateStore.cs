using System.Text.Json;
using MafDb.Core.Memory.Models;

namespace MafDb.Core.Memory.Persistence;

public interface IConversationStateStore
{
    Task<ConversationState?> LoadConversationStateAsync(string sessionId, CancellationToken ct = default);
    Task SaveConversationStateAsync(ConversationState state, CancellationToken ct = default);
    Task<JsonElement?> LoadLegacySessionStateAsync(string sessionId, CancellationToken ct = default);
    Task SaveLegacySessionStateAsync(string sessionId, JsonElement sessionState, CancellationToken ct = default);
}
