using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class DashboardRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<DashboardResumen> ObtenerResumenAsync(
        int? dependenciaId, int? subidoPorUsuarioId, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        await using var cmd = new SqlCommand("dbo.usp_Dashboard_Resumen", con)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubidoPorUsuarioId", (object?)subidoPorUsuarioId ?? DBNull.Value);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw new InvalidOperationException("No se pudo leer el resumen del dashboard.");

        var totalArchivos = r.GetInt32(0);
        var cargasPendientes = r.GetInt32(1);
        var cargasConError = r.GetInt32(2);
        var cargasAprobadas = r.GetInt32(3);

        var ultimos = new List<UltimoCargueRow>();
        if (await r.NextResultAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                ultimos.Add(new UltimoCargueRow(
                    r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    r.GetDateTime(4), r.IsDBNull(5) ? null : r.GetString(5)));
            }
        }

        return new DashboardResumen(totalArchivos, cargasPendientes, cargasConError, cargasAprobadas, ultimos);
    }
}

public sealed record DashboardResumen(
    int TotalArchivos, int CargasPendientes, int CargasConError, int CargasAprobadas,
    IReadOnlyList<UltimoCargueRow> UltimosCargues);

public sealed record UltimoCargueRow(
    int Id, string Dependencia, string Estado, string Archivo, DateTime Fecha, string? Usuario);
