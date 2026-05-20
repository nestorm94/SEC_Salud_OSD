using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class DashboardRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<DashboardResumen> ObtenerResumenAsync(int? dependenciaId, CancellationToken ct = default)
    {
        var filtro = dependenciaId.HasValue ? " AND a.DependenciaId = @DepId" : "";
        var filtroC = dependenciaId.HasValue ? " AND c.DependenciaId = @DepId" : "";

        var sql = $"""
SELECT
 (SELECT COUNT(1) FROM dbo.Archivos a WHERE 1=1{filtro}) AS TotalArchivos,
 (SELECT COUNT(1) FROM dbo.CargasArchivo c WHERE c.Estado IN (N'SUBIDO', N'VALIDANDO', N'VALIDADO_OK'){filtroC}) AS CargasPendientes,
 (SELECT COUNT(1) FROM dbo.CargasArchivo c WHERE c.Estado = N'VALIDADO_CON_ERRORES'{filtroC}) AS CargasConError,
 (SELECT COUNT(1) FROM dbo.CargasArchivo c WHERE c.Estado = N'APROBADO'{filtroC}) AS CargasAprobadas;
""";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (dependenciaId.HasValue)
            cmd.Parameters.AddWithValue("@DepId", dependenciaId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var resumen = new DashboardResumen(
            r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), []);

        var ultimos = $"""
SELECT TOP (10) c.Id, d.Nombre, c.Estado, a.NombreOriginal, c.FechaInicio
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
WHERE 1=1{filtroC}
ORDER BY c.FechaInicio DESC;
""";
        await using var cmd2 = new SqlCommand(ultimos, con);
        if (dependenciaId.HasValue)
            cmd2.Parameters.AddWithValue("@DepId", dependenciaId.Value);
        await using var r2 = await cmd2.ExecuteReaderAsync(ct);
        var ultimosList = new List<UltimoCargueRow>();
        while (await r2.ReadAsync(ct))
        {
            ultimosList.Add(new UltimoCargueRow(
                r2.GetInt32(0), r2.GetString(1), r2.GetString(2),
                r2.GetString(3), r2.GetDateTime(4)));
        }

        return resumen with { UltimosCargues = ultimosList };
    }
}

public sealed record DashboardResumen(
    int TotalArchivos,
    int CargasPendientes,
    int CargasConError,
    int CargasAprobadas,
    IReadOnlyList<UltimoCargueRow> UltimosCargues);

public sealed record UltimoCargueRow(int Id, string Dependencia, string Estado, string Archivo, DateTime Fecha);
