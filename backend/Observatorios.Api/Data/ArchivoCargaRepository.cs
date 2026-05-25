using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

public sealed class ArchivoCargaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    public async Task SincronizarAsync(
        int archivoId, int cargaArchivoId, int usuarioId, int dependenciaId,
        int areaTematicaId, int? plantillaCargaId, string estado, CancellationToken ct = default)
    {
        if (!await TablaExisteAsync(ct)) return;

        const string upsert = """
IF EXISTS (SELECT 1 FROM dbo.ArchivoCarga WHERE ArchivoId = @ArchivoId)
  UPDATE dbo.ArchivoCarga SET Estado = @Estado, AreaTematicaId = @Area, PlantillaCargaId = @Plant
  WHERE ArchivoId = @ArchivoId;
ELSE
  INSERT INTO dbo.ArchivoCarga (ArchivoId, UsuarioId, DependenciaId, AreaTematicaId, PlantillaCargaId, Estado)
  VALUES (@ArchivoId, @UserId, @DepId, @Area, @Plant, @Estado);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(upsert, con);
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@UserId", usuarioId);
        cmd.Parameters.AddWithValue("@DepId", dependenciaId);
        cmd.Parameters.AddWithValue("@Area", areaTematicaId);
        cmd.Parameters.AddWithValue("@Plant", (object?)plantillaCargaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", CargaEstados.Normalizar(estado));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarEstadoPorCargaAsync(int cargaArchivoId, string estado, CancellationToken ct = default)
    {
        if (!await TablaExisteAsync(ct)) return;
        const string sql = """
UPDATE ac SET ac.Estado = @Estado, ac.FechaFin = CASE WHEN @Estado IN (N'APROBADO',N'RECHAZADO',N'VALIDADO_EXITOSO',N'VALIDADO_CON_ERRORES',N'CARGADO_BD')
  THEN SYSUTCDATETIME() ELSE ac.FechaFin END
FROM dbo.ArchivoCarga ac
INNER JOIN dbo.CargasArchivo c ON c.ArchivoId = ac.ArchivoId
WHERE c.Id = @CargaId;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@CargaId", cargaArchivoId);
        cmd.Parameters.AddWithValue("@Estado", CargaEstados.Normalizar(estado));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> TablaExisteAsync(CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(N'dbo.ArchivoCarga', N'U')", con);
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }
}
