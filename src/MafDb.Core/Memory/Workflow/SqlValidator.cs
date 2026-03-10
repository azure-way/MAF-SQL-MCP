using System.Text.RegularExpressions;

namespace MafDb.Core.Memory.Workflow;

public sealed class SqlValidator : ISqlValidator
{
    private static readonly Regex ForbiddenRegex = new(
        @"\b(insert|update|delete|merge|drop|alter|truncate|create|exec|execute|grant|revoke|deny)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SqlValidationResult Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Invalid("SQL is empty.");

        var normalized = sql.Trim();
        if (normalized.StartsWith("```", StringComparison.Ordinal))
            normalized = StripCodeFence(normalized);

        if (normalized.Contains(';'))
        {
            var trimmed = normalized.TrimEnd();
            if (trimmed.Count(c => c == ';') > 1 || !trimmed.EndsWith(';'))
                return Invalid("Multiple SQL statements are not allowed.");

            normalized = trimmed.TrimEnd(';').Trim();
        }

        if (!normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("Only SELECT queries are allowed.");
        }

        if (ForbiddenRegex.IsMatch(normalized))
            return Invalid("Query contains forbidden SQL keywords.");

        return new SqlValidationResult
        {
            IsValid = true,
            NormalizedSql = normalized
        };
    }

    private static SqlValidationResult Invalid(string error)
    {
        return new SqlValidationResult
        {
            IsValid = false,
            Error = error,
            NormalizedSql = string.Empty
        };
    }

    private static string StripCodeFence(string value)
    {
        var lines = value.Split('\n');
        if (lines.Length <= 2)
            return value;

        var body = lines.Skip(1).Take(lines.Length - 2);
        return string.Join('\n', body).Trim();
    }
}
