using System.Security.Cryptography;
using System.Text;
using MafDb.Core.Memory.Models;
using MafDb.Core.Memory.Options;
using MafDb.Core.Tools;

namespace MafDb.Core.Memory.Workflow;

public sealed class SessionSchemaCache : ISchemaCache
{
    private readonly WorkflowOptions _options;
    private readonly Func<CancellationToken, Task<string>> _schemaLoader;

    public SessionSchemaCache(string connectionString, WorkflowOptions options)
        : this(connectionString, options, ct => SqlServerTools.GetDatabaseSchema(connectionString, ct))
    {
    }

    internal SessionSchemaCache(
        string connectionString,
        WorkflowOptions options,
        Func<CancellationToken, Task<string>> schemaLoader)
    {
        _options = options;
        _schemaLoader = schemaLoader;
    }

    public async Task<string> LoadOrRefreshAsync(ConversationState state, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(Math.Max(1, _options.SchemaCacheTtlMinutes));

        if (!string.IsNullOrWhiteSpace(state.CachedSchema) &&
            state.CachedSchemaAtUtc is not null &&
            now - state.CachedSchemaAtUtc.Value < ttl)
        {
            return state.CachedSchema;
        }

        var schema = await _schemaLoader(ct);
        state.CachedSchema = schema;
        state.CachedSchemaHash = ComputeHash(schema);
        state.CachedSchemaAtUtc = now;
        return schema;
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
