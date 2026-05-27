using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

public sealed class ArchivoCargaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    public async Task SincronizarAsync(
        int archivoId, int usuarioId, int dependenciaId,
        int areaTematicaId, int? plantillaCargaId, string estado, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_ArchivoCarga_Sincronizar");
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@DependenciaId", dependenciaId);
        cmd.Parameters.AddWithValue("@AreaTematicaId", areaTematicaId);
        cmd.Parameters.AddWithValue("@PlantillaCargaId", (object?)plantillaCargaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", CargaEstados.Normalizar(estado));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarEstadoPorCargaAsync(int cargaArchivoId, string estado, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
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
