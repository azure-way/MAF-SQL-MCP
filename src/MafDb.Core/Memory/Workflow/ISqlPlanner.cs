namespace MafDb.Core.Memory.Workflow;

public interface ISqlPlanner
{
    Task<SqlPlanResponse> GeneratePlanAsync(SqlPlannerRequest request, CancellationToken ct = default);
    Task<string> ComposeAnswerAsync(string question, string sql, string sqlResult, CancellationToken ct = default);
}
