using System.ComponentModel;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MafDb.Core.Tools;

public static class SqlServerTools
{
    [Description("Gets the database schema including table names, column names and types, primary keys, and foreign key relationships. Call this first to understand the database structure before writing queries.")]
    public static async Task<string> GetDatabaseSchema(string connectionString, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Tables and columns
        const string columnsSql = """
            SELECT
                t.TABLE_SCHEMA,
                t.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.TABLES t
            JOIN INFORMATION_SCHEMA.COLUMNS c
                ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION
            """;

        await using var cmd = new SqlCommand(columnsSql, connection);
        cmd.CommandTimeout = 30;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        string? currentTable = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var fullName = $"{schema}.{table}";

            if (fullName != currentTable)
            {
                if (currentTable != null) sb.AppendLine();
                sb.AppendLine($"TABLE: {fullName}");
                currentTable = fullName;
            }

            var colName = reader.GetString(2);
            var dataType = reader.GetString(3);
            var nullable = reader.GetString(4);
            var maxLen = reader.IsDBNull(5) ? null : reader.GetInt32(5).ToString();

            sb.Append($"  {colName} {dataType}");
            if (maxLen != null) sb.Append($"({maxLen})");
            if (nullable == "NO") sb.Append(" NOT NULL");
            sb.AppendLine();
        }
        await reader.CloseAsync();

        // Primary keys
        sb.AppendLine();
        sb.AppendLine("PRIMARY KEYS:");
        const string pkSql = """
            SELECT
                tc.TABLE_SCHEMA,
                tc.TABLE_NAME,
                STRING_AGG(kcu.COLUMN_NAME, ', ') WITHIN GROUP (ORDER BY kcu.ORDINAL_POSITION) AS KEY_COLUMNS
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            GROUP BY tc.TABLE_SCHEMA, tc.TABLE_NAME
            ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME
            """;

        await using var pkCmd = new SqlCommand(pkSql, connection);
        pkCmd.CommandTimeout = 30;
        await using var pkReader = await pkCmd.ExecuteReaderAsync(cancellationToken);
        while (await pkReader.ReadAsync(cancellationToken))
        {
            sb.AppendLine($"  {pkReader.GetString(0)}.{pkReader.GetString(1)}: ({pkReader.GetString(2)})");
        }
        await pkReader.CloseAsync();

        // Foreign keys
        sb.AppendLine();
        sb.AppendLine("FOREIGN KEYS:");
        const string fkSql = """
            SELECT
                fk.name AS FK_NAME,
                SCHEMA_NAME(tp.schema_id) + '.' + tp.name AS PARENT_TABLE,
                cp.name AS PARENT_COLUMN,
                SCHEMA_NAME(tr.schema_id) + '.' + tr.name AS REFERENCED_TABLE,
                cr.name AS REFERENCED_COLUMN
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
            JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
            JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
            JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
            ORDER BY PARENT_TABLE, FK_NAME
            """;

        await using var fkCmd = new SqlCommand(fkSql, connection);
        fkCmd.CommandTimeout = 30;
        await using var fkReader = await fkCmd.ExecuteReaderAsync(cancellationToken);
        while (await fkReader.ReadAsync(cancellationToken))
        {
            sb.AppendLine($"  {fkReader.GetString(1)}.{fkReader.GetString(2)} -> {fkReader.GetString(3)}.{fkReader.GetString(4)}");
        }

        return sb.ToString();
    }

    [Description("Executes a read-only SQL query against the database and returns results as a formatted table. The query is wrapped in a transaction that is always rolled back for safety. Maximum 100 rows returned.")]
    public static async Task<string> ExecuteSqlQuery(string connectionString, string sqlQuery, CancellationToken cancellationToken = default)
    {
        const int maxRows = 100;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var cmd = new SqlCommand(sqlQuery, connection, transaction);
            cmd.CommandTimeout = 30;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var sb = new StringBuilder();
            var columnCount = reader.FieldCount;

            // Header
            var columnNames = new string[columnCount];
            var columnWidths = new int[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                columnNames[i] = reader.GetName(i);
                columnWidths[i] = columnNames[i].Length;
            }

            // Read rows into buffer to calculate widths
            var rows = new List<string[]>();
            while (await reader.ReadAsync(cancellationToken) && rows.Count < maxRows)
            {
                var row = new string[columnCount];
                for (int i = 0; i < columnCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "NULL";
                    columnWidths[i] = Math.Max(columnWidths[i], row[i].Length);
                }
                rows.Add(row);
            }

            bool hasMore = await reader.ReadAsync(cancellationToken);

            // Format header
            for (int i = 0; i < columnCount; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(columnNames[i].PadRight(columnWidths[i]));
            }
            sb.AppendLine();

            // Separator
            for (int i = 0; i < columnCount; i++)
            {
                if (i > 0) sb.Append("-+-");
                sb.Append(new string('-', columnWidths[i]));
            }
            sb.AppendLine();

            // Rows
            foreach (var row in rows)
            {
                for (int i = 0; i < columnCount; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    sb.Append(row[i].PadRight(columnWidths[i]));
                }
                sb.AppendLine();
            }

            sb.AppendLine($"({rows.Count} row{(rows.Count == 1 ? "" : "s")})");
            if (hasMore)
                sb.AppendLine($"[Results truncated at {maxRows} rows]");

            return sb.ToString();
        }
        finally
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }
}
