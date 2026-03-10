namespace MafDb.Core.Memory.Workflow;

public sealed class SqlValidationResult
{
    public bool IsValid { get; init; }
    public string NormalizedSql { get; init; } = string.Empty;
    public string? Error { get; init; }
}
