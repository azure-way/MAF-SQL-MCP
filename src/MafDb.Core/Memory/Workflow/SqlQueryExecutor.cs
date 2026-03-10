using MafDb.Core.Tools;

namespace MafDb.Core.Memory.Workflow;

public sealed class SqlQueryExecutor : IQueryExecutor
{
    private readonly string _connectionString;

    public SqlQueryExecutor(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<string> ExecuteAsync(string sql, CancellationToken ct = default)
    {
        return SqlServerTools.ExecuteSqlQuery(_connectionString, sql, ct);
    }
}
