using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;
using MafDb.Core.Memory.Persistence;
using MafDb.Core.Memory.Selection;
using MafDb.Core.Memory.Workflow;

namespace MafDb.Core.Tests;

public sealed class SqlWorkflowOrchestratorTests
{
    [Fact]
    public async Task AskAsync_RetriesUpToConfiguredLimit_ThenFails()
    {
        var stateStore = new InMemoryConversationStateStore();
        var memoryOptions = new MemoryOptions();
        var workflowOptions = new WorkflowOptions { MaxRepairRetries = 3 };

        var selector = new ContextSelector(memoryOptions);
        var schemaCache = new FakeSchemaCache();
        var planner = new FakePlanner();
        var validator = new SqlValidator();
        var executor = new AlwaysFailExecutor();

        var orchestrator = new SqlWorkflowOrchestrator(
            stateStore,
            selector,
            schemaCache,
            planner,
            validator,
            executor,
            memoryOptions,
            workflowOptions);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.AskAsync("list sales", "sid"));

        Assert.Contains("after retries", ex.Message);
        Assert.Equal(4, planner.GenerateCalls);
        Assert.Equal(4, executor.ExecuteCalls);
    }

    private sealed class FakeSchemaCache : ISchemaCache
    {
        public Task<string> LoadOrRefreshAsync(ConversationState state, CancellationToken ct = default)
            => Task.FromResult("TABLE: Sales.SalesOrderHeader\n  SalesOrderID int");
    }

    private sealed class FakePlanner : ISqlPlanner
    {
        public int GenerateCalls { get; private set; }

        public Task<SqlPlanResponse> GeneratePlanAsync(SqlPlannerRequest request, CancellationToken ct = default)
        {
            GenerateCalls++;
            return Task.FromResult(new SqlPlanResponse
            {
                Intent = request.Intent,
                Sql = "SELECT TOP 10 * FROM Sales.SalesOrderHeader",
                ReasoningSummary = "test",
                ExpectedColumns = ["SalesOrderID"]
            });
        }

        public Task<string> ComposeAnswerAsync(string question, string sql, string sqlResult, CancellationToken ct = default)
            => Task.FromResult("answer");
    }

    private sealed class AlwaysFailExecutor : IQueryExecutor
    {
        public int ExecuteCalls { get; private set; }

        public Task<string> ExecuteAsync(string sql, CancellationToken ct = default)
        {
            ExecuteCalls++;
            throw new InvalidOperationException("syntax error");
        }
    }
}
