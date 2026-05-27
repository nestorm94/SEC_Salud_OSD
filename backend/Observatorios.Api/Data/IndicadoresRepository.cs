using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

public sealed class IndicadoresRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<IReadOnlyList<IndicadorProstataDto>> ListarProstataAsync(
        string? codigoDane = null,
        string? territorio = null,
        string? regional = null,
        int? anio = null,
        string? area = null,
        int maxRows = 20000,
        CancellationToken ct = default)
    {
        const string vista = "dbo.vw_Tasa_Mortalidad_Prostata_Validada";
        const string spNombre = "usp_Indicador_Prostata_Listar";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await StoredProcedureExisteAsync(con, "dbo", spNombre, ct))
        {
            return await EjecutarProstataDesdeSpAsync(
                con, spNombre, codigoDane, territorio, regional, anio, area, maxRows, ct);
        }

        await using (var cmdObj = new SqlCommand("SELECT OBJECT_ID(@v, N'V')", con))
        {
            cmdObj.Parameters.AddWithValue("@v", vista);
            if ((await cmdObj.ExecuteScalarAsync(ct)) is null)
                return [];
        }

        return await EjecutarProstataLegacyAsync(
            con, vista, codigoDane, territorio, regional, anio, area, maxRows, ct);
    }

    private static async Task<IReadOnlyList<IndicadorProstataDto>> EjecutarProstataDesdeSpAsync(
        SqlConnection con,
        string spNombre,
        string? codigoDane,
        string? territorio,
        string? regional,
        int? anio,
        string? area,
        int maxRows,
        CancellationToken ct)
    {
        var top = Math.Clamp(maxRows, 1, 200000);
        await using var cmd = new SqlCommand($"dbo.{spNombre}", con)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 180
        };
        cmd.Parameters.AddWithValue("@CodigoDane", (object?)codigoDane ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Territorio", (object?)territorio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Regional", (object?)regional ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Anio", (object?)anio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Area", (object?)area ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MaxRows", top);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerRowsProstataAsync(r, top, ct);
    }

    private static async Task<IReadOnlyList<IndicadorProstataDto>> EjecutarProstataLegacyAsync(
        SqlConnection con,
        string vista,
        string? codigoDane,
        string? territorio,
        string? regional,
        int? anio,
        string? area,
        int maxRows,
        CancellationToken ct)
    {
        var top = Math.Clamp(maxRows, 1, 200000);

        var where = new List<string>();
        var ps = new List<SqlParameter>();
        void AddTexto(string col, string param, string? val)
        {
            var v = (val ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v)) return;
            where.Add($"{col} = @{param}");
            ps.Add(new SqlParameter("@" + param, System.Data.SqlDbType.NVarChar, 250) { Value = v });
        }

        AddTexto("[Código DANE]", "codigoDane", codigoDane);
        AddTexto("[Territorio]", "territorio", territorio);
        AddTexto("[Regional]", "regional", regional);
        AddTexto("[Área]", "area", area);
        if (anio is not null)
        {
            where.Add("TRY_CONVERT(int, [Año]) = @anio");
            ps.Add(new SqlParameter("@anio", System.Data.SqlDbType.Int) { Value = anio.Value });
        }
        var whereSql = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);

        var sql = $"""
SELECT
    CAST([Código DANE] AS nvarchar(20)) AS CodigoDane,
    CAST([Territorio] AS nvarchar(250)) AS Territorio,
    CAST([Código-Territorio] AS nvarchar(250)) AS CodigoTerritorio,
    CAST([Regional] AS nvarchar(200)) AS Regional,
    TRY_CONVERT(int, [Año]) AS Anio,
    CAST([Área] AS nvarchar(100)) AS Area,
    CAST([Muertes] AS nvarchar(100)) AS Muertes,
    CAST([Población] AS nvarchar(100)) AS Poblacion,
    CAST([Coeficiente] AS nvarchar(100)) AS Coeficiente,
    CAST([Tasa] AS nvarchar(100)) AS Tasa
FROM {vista}
{whereSql}
ORDER BY [Año], [Territorio];
""";

        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 180 };
        foreach (var p in ps) cmd.Parameters.Add(p);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerRowsProstataAsync(r, top, ct);
    }

    private static async Task<List<IndicadorProstataDto>> LeerRowsProstataAsync(
        SqlDataReader r,
        int top,
        CancellationToken ct)
    {
        var list = new List<IndicadorProstataDto>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new IndicadorProstataDto(
                (r["CodigoDane"]?.ToString() ?? "").Trim(),
                (r["Territorio"]?.ToString() ?? "").Trim(),
                (r["CodigoTerritorio"]?.ToString() ?? "").Trim(),
                (r["Regional"]?.ToString() ?? "").Trim(),
                r["Anio"] is DBNull ? null : Convert.ToInt32(r["Anio"]),
                (r["Area"]?.ToString() ?? "").Trim(),
                ParseDecimalFlexible(r["Muertes"]),
                ParseDecimalFlexible(r["Poblacion"]),
                ParseDecimalFlexible(r["Coeficiente"]),
                ParseDecimalFlexible(r["Tasa"])
            ));
            if (list.Count >= top) break;
        }

        return list;
    }

    private static async Task<bool> StoredProcedureExisteAsync(
        SqlConnection con,
        string schema,
        string procedure,
        CancellationToken ct)
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

    private static decimal? ParseDecimalFlexible(object value)
    {
        if (value is null || value is DBNull) return null;
        var s = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return null;

        var esCo = new System.Globalization.CultureInfo("es-CO");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var clean = s.Replace(" ", "").Replace("\u00A0", "");
        var hasComma = clean.Contains(',');
        var hasDot = clean.Contains('.');

        if (hasComma && hasDot)
        {
            // Usa el último separador como decimal y elimina el otro como miles.
            var normalized = clean.LastIndexOf(',') > clean.LastIndexOf('.')
                ? clean.Replace(".", "").Replace(',', '.')
                : clean.Replace(",", "");

            return decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, inv, out var mixed)
                ? mixed
                : null;
        }

        if (hasComma && !hasDot)
        {
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, esCo, out var dEs)) return dEs;
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, inv, out var dInvComma)) return dInvComma;
            return null;
        }

        if (!hasComma && hasDot)
        {
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, inv, out var dInvDot)) return dInvDot;
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, esCo, out var dEsDot)) return dEsDot;
            return null;
        }

        return decimal.TryParse(clean, System.Globalization.NumberStyles.Any, inv, out var dPlain)
            ? dPlain
            : null;
    }
}
