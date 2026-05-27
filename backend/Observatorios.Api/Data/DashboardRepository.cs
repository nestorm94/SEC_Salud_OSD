using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class DashboardRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<DashboardResumen> ObtenerResumenAsync(
        int? dependenciaId,
        int? subidoPorUsuarioId,
        CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        // Adoptamos stored procedures gradualmente (con fallback al SQL legacy si aún no existen).
        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Dashboard_Resumen", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Dashboard_Resumen", con)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SubidoPorUsuarioId", (object?)subidoPorUsuarioId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new InvalidOperationException("No se pudo leer el resumen del dashboard.");

            var totalArchivos_sp = r.GetInt32(0);
            var cargasPendientes_sp = r.GetInt32(1);
            var cargasConError_sp = r.GetInt32(2);
            var cargasAprobadas_sp = r.GetInt32(3);

            var ultimosList_sp = new List<UltimoCargueRow>();
            if (await r.NextResultAsync(ct))
            {
                while (await r.ReadAsync(ct))
                {
                    ultimosList_sp.Add(new UltimoCargueRow(
                        r.GetInt32(0), r.GetString(1), r.GetString(2),
                        r.GetString(3), r.GetDateTime(4),
                        r.IsDBNull(5) ? null : r.GetString(5)));
                }
            }

            return new DashboardResumen(
                totalArchivos_sp, cargasPendientes_sp, cargasConError_sp, cargasAprobadas_sp, ultimosList_sp);
        }

        var filtroA = dependenciaId.HasValue ? " AND a.DependenciaId = @DepId" : "";
        var filtroC = dependenciaId.HasValue ? " AND c.DependenciaId = @DepId" : "";
        var filtroUserA = subidoPorUsuarioId.HasValue ? " AND a.SubidoPorUsuarioId = @UserId" : "";
        var filtroUserC = subidoPorUsuarioId.HasValue ? " AND c.UsuarioId = @UserId" : "";

        var sqlResumen = $"""
SELECT
 (SELECT COUNT(1) FROM dbo.Archivos a WHERE 1=1{filtroA}{filtroUserA}) AS TotalArchivos,
 (SELECT COUNT(1) FROM dbo.CargasArchivo c WHERE c.Estado IN (
    N'RECIBIDO', N'EN_VALIDACION', N'SUBIDO', N'VALIDANDO'
 ){filtroC}{filtroUserC})
 + (SELECT COUNT(1) FROM dbo.Archivos a WHERE a.Estado IN (N'PendienteValidacion', N'Validado'){filtroA}{filtroUserA}) AS CargasPendientes,
 (SELECT COUNT(1) FROM dbo.CargasArchivo c WHERE c.Estado = N'VALIDADO_CON_ERRORES'{filtroC}{filtroUserC})
 + (SELECT COUNT(1) FROM dbo.Archivos a WHERE a.Estado = N'Rechazado'{filtroA}{filtroUserA}) AS CargasConError,
 (SELECT COUNT(1) FROM dbo.CargasArchivo c WHERE c.Estado = N'APROBADO'{filtroC}{filtroUserC}) AS CargasAprobadas;
""";

        var sqlUltimos = $"""
SELECT TOP (10)
    c.Id,
    d.Nombre,
    c.Estado,
    a.NombreOriginal,
    COALESCE(c.FechaFin, c.FechaInicio) AS FechaActividad,
    u.NombreUsuario
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
LEFT JOIN dbo.Usuarios u ON u.Id = c.UsuarioId
WHERE 1=1{filtroC}{filtroUserC}
ORDER BY COALESCE(c.FechaFin, c.FechaInicio) DESC, c.Id DESC;
""";

        int totalArchivos, cargasPendientes, cargasConError, cargasAprobadas;
        await using (var cmd = new SqlCommand(sqlResumen, con))
        {
            if (dependenciaId.HasValue) cmd.Parameters.AddWithValue("@DepId", dependenciaId.Value);
            if (subidoPorUsuarioId.HasValue) cmd.Parameters.AddWithValue("@UserId", subidoPorUsuarioId.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new InvalidOperationException("No se pudo leer el resumen del dashboard.");
            totalArchivos = r.GetInt32(0);
            cargasPendientes = r.GetInt32(1);
            cargasConError = r.GetInt32(2);
            cargasAprobadas = r.GetInt32(3);
        }

        var ultimosList = new List<UltimoCargueRow>();
        await using (var cmd2 = new SqlCommand(sqlUltimos, con))
        {
            if (dependenciaId.HasValue) cmd2.Parameters.AddWithValue("@DepId", dependenciaId.Value);
            if (subidoPorUsuarioId.HasValue) cmd2.Parameters.AddWithValue("@UserId", subidoPorUsuarioId.Value);
            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
            {
                ultimosList.Add(new UltimoCargueRow(
                    r2.GetInt32(0), r2.GetString(1), r2.GetString(2),
                    r2.GetString(3), r2.GetDateTime(4),
                    r2.IsDBNull(5) ? null : r2.GetString(5)));
            }
        }

        return new DashboardResumen(
            totalArchivos, cargasPendientes, cargasConError, cargasAprobadas, ultimosList);
    }
}

public sealed record DashboardResumen(
    int TotalArchivos,
    int CargasPendientes,
    int CargasConError,
    int CargasAprobadas,
    IReadOnlyList<UltimoCargueRow> UltimosCargues);

public sealed record UltimoCargueRow(
    int Id,
    string Dependencia,
    string Estado,
    string Archivo,
    DateTime Fecha,
    string? Usuario);
