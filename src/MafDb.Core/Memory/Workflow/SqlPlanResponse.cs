namespace MafDb.Core.Memory.Workflow;

public sealed class SqlPlanResponse
{
    public string Intent { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string ReasoningSummary { get; set; } = string.Empty;
    public string[] ExpectedColumns { get; set; } = [];
}
