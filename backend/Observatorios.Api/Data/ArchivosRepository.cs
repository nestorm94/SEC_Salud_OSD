using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

public sealed class ArchivosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<int> InsertAsync(ArchivoInsert row, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_Insertar");
        cmd.Parameters.AddWithValue("@DependenciaId", row.DependenciaId);
        cmd.Parameters.AddWithValue("@LineaTematicaId", row.LineaTematicaId);
        cmd.Parameters.AddWithValue("@IndicadorId", row.IndicadorId);
        cmd.Parameters.AddWithValue("@NombreOriginal", row.NombreOriginal);
        cmd.Parameters.AddWithValue("@NombreAlmacenado", row.NombreAlmacenado);
        cmd.Parameters.AddWithValue("@RutaRelativa", row.RutaRelativa);
        cmd.Parameters.AddWithValue("@TipoMime", (object?)row.TipoMime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TamanoBytes", (object?)row.TamanoBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubidoPorUsuarioId", (object?)row.SubidoPorUsuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Observaciones", (object?)row.Observaciones ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)row.Estado ?? ArchivoEstados.PendienteValidacion);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarResultadoValidacionAsync(int id, string estado, string? erroresJson, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_ActualizarValidacion");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Estado", estado);
        cmd.Parameters.AddWithValue("@ErroresValidacionJson", (object?)erroresJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarcarEnviadoAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_MarcarEnviado");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Estado", ArchivoEstados.Enviado);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ArchivoEstadoRow?> GetEstadoAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_ObtenerEstado");
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerEstado(r) : null;
    }

    public async Task<IReadOnlyList<ArchivoListaRow>> ListAsync(
        int? dependenciaIdFiltro,
        int? lineaTematicaIdFiltro = null,
        int? subidoPorUsuarioIdFiltro = null,
        CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_Listar");
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LineaTematicaId", (object?)lineaTematicaIdFiltro ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubidoPorUsuarioId", (object?)subidoPorUsuarioIdFiltro ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerListaAsync(r, ct);
    }

    public async Task<ArchivoFullRow?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_Obtener");
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerFull(r) : null;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Archivo_Eliminar");
        cmd.Parameters.AddWithValue("@Id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };

    private static ArchivoEstadoRow LeerEstado(SqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetInt32(2), r.GetInt32(3),
            r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetInt32(6),
            r.IsDBNull(7) ? null : r.GetInt32(7), r.IsDBNull(8) ? null : r.GetString(8));

    private static ArchivoFullRow LeerFull(SqlDataReader r) =>
        new(r.GetInt32(0), r.GetInt32(1), r.IsDBNull(2) ? null : r.GetInt32(2),
            r.IsDBNull(3) ? null : r.GetInt32(3), r.GetString(4), r.GetString(5), r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetInt64(8),
            r.GetDateTime(9), r.IsDBNull(10) ? null : r.GetString(10),
            r.IsDBNull(11) ? null : r.GetString(11), r.IsDBNull(12) ? null : r.GetString(12),
            r.IsDBNull(13) ? null : r.GetString(13), r.IsDBNull(14) ? null : r.GetInt32(14),
            r.IsDBNull(15) ? ArchivoEstados.PendienteValidacion : r.GetString(15),
            r.IsDBNull(16) ? null : r.GetDateTime(16), r.IsDBNull(17) ? null : r.GetDateTime(17),
            r.IsDBNull(18) ? null : r.GetString(18));

    private static async Task<IReadOnlyList<ArchivoListaRow>> LeerListaAsync(SqlDataReader r, CancellationToken ct)
    {
        var list = new List<ArchivoListaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new ArchivoListaRow(
                r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt32(3), r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6),
                r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetInt64(9),
                r.GetDateTime(10), r.IsDBNull(11) ? null : r.GetString(11), r.IsDBNull(12) ? null : r.GetString(12),
                r.GetString(13), r.IsDBNull(14) ? ArchivoEstados.PendienteValidacion : r.GetString(14),
                r.IsDBNull(15) ? null : r.GetDateTime(15), r.IsDBNull(16) ? null : r.GetDateTime(16)));
        }
        return list;
    }
}

public sealed record ArchivoInsert(
    int DependenciaId, int LineaTematicaId, int IndicadorId,
    string NombreOriginal, string NombreAlmacenado, string RutaRelativa,
    string? TipoMime, long? TamanoBytes, int? SubidoPorUsuarioId,
    string? Observaciones, string? Estado = null);

public sealed record ArchivoEstadoRow(
    int Id, string Estado, int? SubidoPorUsuarioId, int DependenciaId,
    string RutaRelativa, string NombreOriginal, int? LineaTematicaId, int? IndicadorId, string? Observaciones);

public sealed record ArchivoListaRow(
    int Id, int DependenciaId, string DependenciaNombre,
    int? LineaTematicaId, string? LineaTematicaNombre, int? IndicadorId, string? IndicadorNombre,
    string NombreOriginal, string? TipoMime, long? TamanoBytes, DateTime CreadoEn, string? SubidoPor,
    string? Observaciones, string RutaRelativa, string Estado, DateTime? FechaValidacion, DateTime? FechaEnvio);

public sealed record ArchivoFullRow(
    int Id, int DependenciaId, int? LineaTematicaId, int? IndicadorId,
    string NombreOriginal, string NombreAlmacenado, string RutaRelativa, string? TipoMime, long? TamanoBytes,
    DateTime CreadoEn, string? Observaciones, string? LineaTematicaNombre, string? IndicadorNombre,
    string? SubidoPor, int? SubidoPorUsuarioId, string Estado, DateTime? FechaValidacion,
    DateTime? FechaEnvio, string? ErroresValidacionJson);
