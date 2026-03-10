using MafDb.Core.Memory.Models;

namespace MafDb.Core.Memory.Selection;

public interface IContextSelector
{
    ContextSelectionResult Select(ConversationState state, string question);
}
