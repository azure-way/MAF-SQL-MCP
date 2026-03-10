using MafDb.Core.Memory.Models;

namespace MafDb.Core.Memory.Workflow;

public interface ISqlWorkflowOrchestrator
{
    Task<WorkflowAskResult> AskAsync(string question, string? sessionId = null, CancellationToken ct = default);
}
