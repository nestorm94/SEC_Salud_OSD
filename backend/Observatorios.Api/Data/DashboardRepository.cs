using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>
/// Agrega métricas del panel principal del OSD: totales de archivos, cargas por estado
/// y actividad reciente unificada de cargues y archivos pendientes.
/// </summary>
public sealed class DashboardRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    /// <summary>
    /// Obtiene contadores del dashboard y los últimos movimientos de carga/archivo.
    /// </summary>
    /// <param name="dependenciaId">Filtra por dependencia; null para vista global.</param>
    /// <param name="subidoPorUsuarioId">Filtra archivos/cargas del usuario; null sin filtro.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resumen con totales y lista de actividad reciente.</returns>
    public async Task<DashboardResumen> ObtenerResumenAsync(
        int? dependenciaId, int? subidoPorUsuarioId, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        int totalArchivos;
        int cargasPendientes;
        int cargasConError;
        int cargasAprobadas;

        /* SP usp_Dashboard_Resumen: contadores agregados de archivos y cargas por estado. */
        await using (var cmd = new SqlCommand("dbo.usp_Dashboard_Resumen", con)
                     {
                         CommandType = CommandType.StoredProcedure
                     })
        {
            cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SubidoPorUsuarioId", (object?)subidoPorUsuarioId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new InvalidOperationException("No se pudo leer el resumen del dashboard.");

            totalArchivos = r.GetInt32(0);
            cargasPendientes = r.GetInt32(1);
            cargasConError = r.GetInt32(2);
            cargasAprobadas = r.GetInt32(3);
        }

        /* Actividad reciente: consulta unificada (no depende del 2.º result set del SP, que puede estar desactualizado o con TOP). */
        var ultimos = await ObtenerActividadRecienteAsync(con, dependenciaId, subidoPorUsuarioId, ct);

        return new DashboardResumen(totalArchivos, cargasPendientes, cargasConError, cargasAprobadas, ultimos);
    }

    private async Task<IReadOnlyList<UltimoCargueRow>> ObtenerActividadRecienteAsync(
        SqlConnection con,
        int? dependenciaId,
        int? subidoPorUsuarioId,
        CancellationToken ct)
    {
        /* UNION de dbo.CargasArchivo y dbo.Archivos no enviados para actividad reciente del panel. */
        const string sql = """
            SELECT Id, Origen, Dependencia, Estado, Archivo, Fecha, Usuario
            FROM (
                SELECT
                    c.Id,
                    N'Cargue' AS Origen,
                    c.DependenciaId,
                    d.Nombre AS Dependencia,
                    c.Estado,
                    a.NombreOriginal AS Archivo,
                    COALESCE(c.FechaFin, c.FechaInicio) AS Fecha,
                    c.UsuarioId,
                    u.NombreUsuario AS Usuario
                FROM dbo.CargasArchivo c
                INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
                INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
                LEFT JOIN dbo.Usuarios u ON u.Id = c.UsuarioId

                UNION ALL

                SELECT
                    a.Id,
                    N'Archivo' AS Origen,
                    a.DependenciaId,
                    d.Nombre AS Dependencia,
                    COALESCE(a.Estado, N'PendienteValidacion') AS Estado,
                    a.NombreOriginal AS Archivo,
                    COALESCE(a.FechaValidacion, a.CreadoEn) AS Fecha,
                    a.SubidoPorUsuarioId AS UsuarioId,
                    u.NombreUsuario AS Usuario
                FROM dbo.Archivos a
                INNER JOIN dbo.Dependencias d ON d.Id = a.DependenciaId
                LEFT JOIN dbo.Usuarios u ON u.Id = a.SubidoPorUsuarioId
                WHERE a.Estado IS NULL OR a.Estado <> N'Enviado'
            ) act
            WHERE (@DependenciaId IS NULL OR act.DependenciaId = @DependenciaId)
              AND (@SubidoPorUsuarioId IS NULL OR act.UsuarioId = @SubidoPorUsuarioId)
            ORDER BY act.Fecha DESC, act.Id DESC, act.Origen DESC;
            """;

        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubidoPorUsuarioId", (object?)subidoPorUsuarioId ?? DBNull.Value);

        var list = new List<UltimoCargueRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new UltimoCargueRow(
                r.GetInt32(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetDateTime(5),
                r.IsDBNull(6) ? null : r.GetString(6)));
        }

        return list;
    }
}

/// <summary>Contadores y actividad reciente retornados al panel del OSD.</summary>
public sealed record DashboardResumen(
    int TotalArchivos, int CargasPendientes, int CargasConError, int CargasAprobadas,
    IReadOnlyList<UltimoCargueRow> UltimosCargues);

/// <summary>Fila de actividad reciente (cargue o archivo) en el dashboard.</summary>
public sealed record UltimoCargueRow(
    int Id, string Origen, string Dependencia, string Estado, string Archivo, DateTime Fecha, string? Usuario);
