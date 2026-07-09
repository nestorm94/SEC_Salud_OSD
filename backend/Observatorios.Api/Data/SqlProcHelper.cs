namespace Observatorios.Api.Data;

/// <summary>Utilidades para parámetros CSV en stored procedures de usuarios.</summary>
internal static class SqlProcHelper
{
    /// <summary>Serializa roles a CSV para parámetros de SP de usuarios.</summary>
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
