using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>Utilidades para adopción incremental de stored procedures.</summary>
internal static class SqlProcHelper
{
    public static async Task<bool> StoredProcedureExisteAsync(
        SqlConnection con,
        string schema,
        string procedure,
        CancellationToken ct = default)
    {
        const string sql = """
SELECT 1
FROM sys.procedures p
INNER JOIN sys.schemas s ON s.schema_id = p.schema_id
WHERE s.name = @schema AND p.name = @procedure;
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@procedure", procedure);
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }

    public static string RolesToCsv(IEnumerable<string> roles) =>
        string.Join(',', roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<string> RolesFromCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string AreaIdsToCsv(IEnumerable<int> ids) =>
        string.Join(',', ids.Distinct());

    public static IReadOnlyList<int> AreaIdsFromCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        var list = new List<int>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var id))
                list.Add(id);
        }
        return list;
    }
}
