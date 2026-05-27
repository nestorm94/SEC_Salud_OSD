using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

public sealed class IndicadoresRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<IReadOnlyList<IndicadorProstataDto>> ListarProstataAsync(
        string? codigoDane = null, string? territorio = null, string? regional = null,
        int? anio = null, string? area = null, int maxRows = 20000, CancellationToken ct = default)
    {
        var top = Math.Clamp(maxRows, 1, 200000);
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand("dbo.usp_Indicador_Prostata_Listar", con)
        {
            CommandType = CommandType.StoredProcedure,
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

    private static async Task<List<IndicadorProstataDto>> LeerRowsProstataAsync(
        SqlDataReader r, int top, CancellationToken ct)
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
                ParseDecimalFlexible(r["Tasa"])));
            if (list.Count >= top) break;
        }
        return list;
    }

    private static decimal? ParseDecimalFlexible(object value)
    {
        if (value is null or DBNull) return null;
        var s = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return null;

        var esCo = new CultureInfo("es-CO");
        var inv = CultureInfo.InvariantCulture;
        var clean = s.Replace(" ", "").Replace("\u00A0", "");
        var hasComma = clean.Contains(',');
        var hasDot = clean.Contains('.');

        if (hasComma && hasDot)
        {
            var normalized = clean.LastIndexOf(',') > clean.LastIndexOf('.')
                ? clean.Replace(".", "").Replace(',', '.')
                : clean.Replace(",", "");
            return decimal.TryParse(normalized, NumberStyles.Any, inv, out var mixed) ? mixed : null;
        }

        if (hasComma && !hasDot)
        {
            if (decimal.TryParse(clean, NumberStyles.Any, esCo, out var dEs)) return dEs;
            if (decimal.TryParse(clean, NumberStyles.Any, inv, out var dInvComma)) return dInvComma;
            return null;
        }

        if (!hasComma && hasDot)
        {
            if (decimal.TryParse(clean, NumberStyles.Any, inv, out var dInvDot)) return dInvDot;
            if (decimal.TryParse(clean, NumberStyles.Any, esCo, out var dEsDot)) return dEsDot;
            return null;
        }

        return decimal.TryParse(clean, NumberStyles.Any, inv, out var dPlain) ? dPlain : null;
    }
}
