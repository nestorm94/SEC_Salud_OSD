using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

/// <summary>
/// Sincroniza el vínculo entre un archivo Excel subido y su registro de carga en dbo.CargasArchivo.
/// </summary>
public sealed class ArchivoCargaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    /// <summary>
    /// Crea o actualiza la relación archivo-carga con estado inicial del flujo.
    /// </summary>
    public async Task SincronizarAsync(
        int archivoId, int usuarioId, int dependenciaId,
        int areaTematicaId, int? plantillaCargaId, string estado, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        /* SP usp_ArchivoCarga_Sincronizar */
        await using var cmd = Sp(con, "dbo.usp_ArchivoCarga_Sincronizar");
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@DependenciaId", dependenciaId);
        cmd.Parameters.AddWithValue("@AreaTematicaId", areaTematicaId);
        cmd.Parameters.AddWithValue("@PlantillaCargaId", (object?)plantillaCargaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", CargaEstados.Normalizar(estado));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Propaga el estado de la carga al registro de archivo asociado.</summary>
    public async Task ActualizarEstadoPorCargaAsync(int cargaArchivoId, string estado, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        /* SP usp_ArchivoCarga_ActualizarEstadoPorCarga */
        await using var cmd = Sp(con, "dbo.usp_ArchivoCarga_ActualizarEstadoPorCarga");
        cmd.Parameters.AddWithValue("@CargaArchivoId", cargaArchivoId);
        cmd.Parameters.AddWithValue("@Estado", CargaEstados.Normalizar(estado));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };
}
