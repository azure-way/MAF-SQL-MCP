namespace MafDb.Core.Memory.Workflow;

public interface IQueryExecutor
{
    Task<string> ExecuteAsync(string sql, CancellationToken ct = default);
}
