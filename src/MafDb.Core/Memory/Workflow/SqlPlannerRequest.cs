namespace MafDb.Core.Memory.Workflow;

public sealed class SqlPlannerRequest
{
    public string UserQuestion { get; init; } = string.Empty;
    public string Intent { get; init; } = string.Empty;
    public string SelectedContext { get; init; } = string.Empty;
    public string DatabaseSchema { get; init; } = string.Empty;
    public string? PreviousSql { get; init; }
    public string? PreviousError { get; init; }
}
