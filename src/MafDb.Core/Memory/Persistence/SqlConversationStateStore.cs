using System.Text.Json;
using MafDb.Core.Memory.Models;
using Microsoft.Data.SqlClient;

namespace MafDb.Core.Memory.Persistence;

public sealed class SqlConversationStateStore : IConversationStateStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaEnsured;

    public SqlConversationStateStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ConversationState?> LoadConversationStateAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT ConversationState FROM dbo.AgentSessions WHERE SessionId = @SessionId";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
            return null;

        return JsonSerializer.Deserialize<ConversationState>((string)result);
    }

    public async Task SaveConversationStateAsync(ConversationState state, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var json = JsonSerializer.Serialize(state);

        const string sql = """
            MERGE dbo.AgentSessions AS target
            USING (SELECT @SessionId AS SessionId) AS source
            ON target.SessionId = source.SessionId
            WHEN MATCHED THEN
                UPDATE SET ConversationState = @ConversationState, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (SessionId, ConversationState, CreatedAt, UpdatedAt)
                VALUES (@SessionId, @ConversationState, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SessionId", state.SessionId);
        cmd.Parameters.AddWithValue("@ConversationState", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<JsonElement?> LoadLegacySessionStateAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

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

    public async Task SaveLegacySessionStateAsync(string sessionId, JsonElement sessionState, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var json = sessionState.GetRawText();

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

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaEnsured)
            return;

        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_schemaEnsured)
                return;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = """
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentSessions' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    CREATE TABLE dbo.AgentSessions
                    (
                        SessionId NVARCHAR(64) NOT NULL,
                        SessionState NVARCHAR(MAX) NULL,
                        ConversationState NVARCHAR(MAX) NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT PK_AgentSessions PRIMARY KEY (SessionId)
                    );
                END;

                IF COL_LENGTH('dbo.AgentSessions', 'ConversationState') IS NULL
                BEGIN
                    ALTER TABLE dbo.AgentSessions
                    ADD ConversationState NVARCHAR(MAX) NULL;
                END;

                IF COL_LENGTH('dbo.AgentSessions', 'SessionState') IS NULL
                BEGIN
                    ALTER TABLE dbo.AgentSessions
                    ADD SessionState NVARCHAR(MAX) NULL;
                END;
                """;

            await using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(ct);

            _schemaEnsured = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
