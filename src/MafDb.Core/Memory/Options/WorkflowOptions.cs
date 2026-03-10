namespace MafDb.Core.Memory.Options;

public enum WorkflowMode
{
    Deterministic,
    ToolCalling
}

public sealed class WorkflowOptions
{
    public WorkflowMode Mode { get; set; } = WorkflowMode.Deterministic;
    public int MaxRepairRetries { get; set; } = 3;
    public int SchemaCacheTtlMinutes { get; set; } = 30;
    public bool ReturnSqlInUserText { get; set; }
    public bool FailClosedOnValidation { get; set; } = true;
}
