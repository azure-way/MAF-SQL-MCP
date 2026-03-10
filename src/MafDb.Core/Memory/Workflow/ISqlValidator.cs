namespace MafDb.Core.Memory.Workflow;

public interface ISqlValidator
{
    SqlValidationResult Validate(string sql);
}
