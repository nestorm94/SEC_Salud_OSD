using System.Text;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>Ejecuta scripts .sql divididos por <c>GO</c> (línea sola).</summary>
internal static class SqlScriptRunner
{
    private static readonly int[] IgnoredSqlNumbers = [2714, 1913, 2705];

    public static async Task ExecuteFileAsync(
        SqlConnection con,
        string filePath,
        int commandTimeoutSeconds = 120,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encontró el script SQL: {filePath}", filePath);

        var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        foreach (var batch in SplitBatches(text))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;

            await using var cmd = new SqlCommand(batch, con) { CommandTimeout = commandTimeoutSeconds };
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (SqlException ex) when (IgnoredSqlNumbers.Contains(ex.Number))
            {
                // Objeto ya existe / columna duplicada en re-ejecución idempotente
            }
        }
    }

    public static IEnumerable<string> SplitBatches(string script)
    {
        var sb = new StringBuilder();
        using var reader = new StringReader(script);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                continue;
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0)
            yield return sb.ToString();
    }
}
