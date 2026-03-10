using MafDb.Core.Memory.Models;

namespace MafDb.Core.Memory.Workflow;

public interface ISchemaCache
{
    Task<string> LoadOrRefreshAsync(ConversationState state, CancellationToken ct = default);
}
