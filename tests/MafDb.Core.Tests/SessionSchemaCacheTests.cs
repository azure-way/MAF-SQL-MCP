using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;
using MafDb.Core.Memory.Workflow;

namespace MafDb.Core.Tests;

public sealed class SessionSchemaCacheTests
{
    [Fact]
    public async Task LoadOrRefresh_UsesCacheWithinTtl_AndRefreshesAfterExpiry()
    {
        var calls = 0;
        Task<string> Loader(CancellationToken _)
        {
            calls++;
            return Task.FromResult("schema-v" + calls);
        }

        var options = new WorkflowOptions { SchemaCacheTtlMinutes = 30 };
        var cache = new SessionSchemaCache("cs", options, Loader);
        var state = new ConversationState { SessionId = "s1" };

        var first = await cache.LoadOrRefreshAsync(state);
        var second = await cache.LoadOrRefreshAsync(state);

        state.CachedSchemaAtUtc = DateTimeOffset.UtcNow.AddMinutes(-31);
        var third = await cache.LoadOrRefreshAsync(state);

        Assert.Equal("schema-v1", first);
        Assert.Equal("schema-v1", second);
        Assert.Equal("schema-v2", third);
        Assert.Equal(2, calls);
        Assert.False(string.IsNullOrWhiteSpace(state.CachedSchemaHash));
    }
}
