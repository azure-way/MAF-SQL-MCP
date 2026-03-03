using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace MafDb.Core.Persistence;

public sealed class SqlSessionStore : ISessionStore
{
    private readonly string _connectionString;

    public SqlSessionStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SaveAsync(string sessionId, JsonElement sessionState, CancellationToken ct = default)
    {
        var json = sessionState.GetRawText();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            MERGE dbo.AgentSessions AS target
            USING (SELECT @SessionId AS SessionId) AS source
            ON target.SessionId = source.SessionId
            WHEN MATCHED THEN
                UPDATE SET SessionState = @SessionState, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (SessionId, SessionState, CreatedAt, UpdatedAt)
                VALUES (@SessionId, @SessionState, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);
        cmd.Parameters.AddWithValue("@SessionState", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<JsonElement?> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT SessionState FROM dbo.AgentSessions WHERE SessionId = @SessionId";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
            return null;

        return JsonDocument.Parse((string)result).RootElement;
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT 1 FROM dbo.AgentSessions WHERE SessionId = @SessionId";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }
}
