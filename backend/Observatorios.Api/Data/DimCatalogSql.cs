using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>Resuelve nombres de columnas en dim_departamento / dim_municipio (esquemas legacy y nuevos).</summary>
internal static class DimCatalogSql
{
    public const string CodigoDepartamentoCasanare = "85";

    public static async Task<bool> ObjetoExisteAsync(SqlConnection con, string objeto, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@t, N'U')", con);
        cmd.Parameters.AddWithValue("@t", objeto);
        var o = await cmd.ExecuteScalarAsync(ct);
        if (o is not null && o != DBNull.Value) return true;

        await using var cmd2 = new SqlCommand("SELECT OBJECT_ID(@t, N'V')", con);
        cmd2.Parameters.AddWithValue("@t", objeto);
        var o2 = await cmd2.ExecuteScalarAsync(ct);
        return o2 is not null && o2 != DBNull.Value;
    }

    public static Task<bool> TablaExisteAsync(SqlConnection con, string objeto, CancellationToken ct) =>
        ObjetoExisteAsync(con, objeto, ct);

    public static async Task<bool> ColumnaExisteAsync(SqlConnection con, string tablaSql, string columna, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("""
SELECT 1 FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@t) AND c.name = @c
""", con);
        cmd.Parameters.AddWithValue("@t", tablaSql);
        cmd.Parameters.AddWithValue("@c", columna);
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }

    public static async Task<string?> ColumnaCodigoMunicipioAsync(SqlConnection con, string tabla, CancellationToken ct)
    {
        foreach (var c in new[] { "codigo_dane", "codigo_municipio", "cod_municipio" })
        {
            if (await ColumnaExisteAsync(con, tabla, c, ct))
                return c;
        }
        return null;
    }

    public static async Task<string?> ColumnaDepartamentoMunicipioAsync(SqlConnection con, string tabla, CancellationToken ct)
    {
        foreach (var c in new[] { "cod_departamento", "codigo_departamento" })
        {
            if (await ColumnaExisteAsync(con, tabla, c, ct))
                return c;
        }
        return null;
    }
}
