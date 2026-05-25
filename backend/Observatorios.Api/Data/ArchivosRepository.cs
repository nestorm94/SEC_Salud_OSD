using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

/// <summary>Metadata de archivos físicos por dependencia y línea temática.</summary>
public sealed class ArchivosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<int> InsertAsync(ArchivoInsert row, CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.Archivos
    (DependenciaId, LineaTematicaId, IndicadorId, NombreOriginal, NombreAlmacenado,
     RutaRelativa, TipoMime, TamanoBytes, SubidoPorUsuarioId, Observaciones, Estado)
OUTPUT INSERTED.Id
VALUES (@DepId, @LineaId, @IndId, @NombreOriginal, @NombreAlmacenado, @Ruta, @Mime, @Tam, @UserId, @Obs, @Estado);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@DepId", row.DependenciaId);
        cmd.Parameters.AddWithValue("@LineaId", row.LineaTematicaId);
        cmd.Parameters.AddWithValue("@IndId", row.IndicadorId);
        cmd.Parameters.AddWithValue("@NombreOriginal", row.NombreOriginal);
        cmd.Parameters.AddWithValue("@NombreAlmacenado", row.NombreAlmacenado);
        cmd.Parameters.AddWithValue("@Ruta", row.RutaRelativa);
        cmd.Parameters.AddWithValue("@Mime", (object?)row.TipoMime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Tam", (object?)row.TamanoBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UserId", (object?)row.SubidoPorUsuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Obs", (object?)row.Observaciones ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", row.Estado ?? ArchivoEstados.PendienteValidacion);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarResultadoValidacionAsync(
        int id,
        string estado,
        string? erroresJson,
        CancellationToken ct = default)
    {
        const string sql = """
UPDATE dbo.Archivos
SET Estado = @Estado,
    FechaValidacion = SYSUTCDATETIME(),
    ErroresValidacionJson = @Errores
WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Estado", estado);
        cmd.Parameters.AddWithValue("@Errores", (object?)erroresJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarcarEnviadoAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
UPDATE dbo.Archivos
SET Estado = @Estado,
    FechaEnvio = SYSUTCDATETIME()
WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Estado", ArchivoEstados.Enviado);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ArchivoEstadoRow?> GetEstadoAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
SELECT Id, Estado, SubidoPorUsuarioId, DependenciaId, RutaRelativa, NombreOriginal,
       LineaTematicaId, IndicadorId, Observaciones
FROM dbo.Archivos WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return new ArchivoEstadoRow(
            r.GetInt32(0),
            r.GetString(1),
            r.IsDBNull(2) ? null : r.GetInt32(2),
            r.GetInt32(3),
            r.GetString(4),
            r.GetString(5),
            r.IsDBNull(6) ? null : r.GetInt32(6),
            r.IsDBNull(7) ? null : r.GetInt32(7),
            r.IsDBNull(8) ? null : r.GetString(8));
    }

    public async Task<IReadOnlyList<ArchivoListaRow>> ListAsync(
        int? dependenciaIdFiltro,
        int? lineaTematicaIdFiltro = null,
        int? subidoPorUsuarioIdFiltro = null,
        CancellationToken ct = default)
    {
        var sql = """
SELECT TOP (300) a.Id, a.DependenciaId, d.Nombre AS DependenciaNombre,
       a.LineaTematicaId, lt.Nombre AS LineaTematicaNombre,
       a.IndicadorId, ind.Nombre AS IndicadorNombre,
       a.NombreOriginal, a.TipoMime, a.TamanoBytes, a.CreadoEn, u.NombreUsuario,
       a.Observaciones, a.RutaRelativa, a.Estado, a.FechaValidacion, a.FechaEnvio
FROM dbo.Archivos a
INNER JOIN dbo.Dependencias d ON d.Id = a.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = a.LineaTematicaId
LEFT JOIN dbo.Indicador ind ON ind.Id = a.IndicadorId
LEFT JOIN dbo.Usuarios u ON u.Id = a.SubidoPorUsuarioId
WHERE 1=1
""";
        if (dependenciaIdFiltro.HasValue)
            sql += " AND a.DependenciaId = @DepId";
        if (lineaTematicaIdFiltro.HasValue)
            sql += " AND a.LineaTematicaId = @LineaId";
        if (subidoPorUsuarioIdFiltro.HasValue)
            sql += " AND a.SubidoPorUsuarioId = @UserId";
        sql += " ORDER BY a.Id DESC;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (dependenciaIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        if (lineaTematicaIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@LineaId", lineaTematicaIdFiltro.Value);
        if (subidoPorUsuarioIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@UserId", subidoPorUsuarioIdFiltro.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ArchivoListaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new ArchivoListaRow(
                r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt32(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8),
                r.IsDBNull(9) ? null : r.GetInt64(9),
                r.GetDateTime(10),
                r.IsDBNull(11) ? null : r.GetString(11),
                r.IsDBNull(12) ? null : r.GetString(12),
                r.GetString(13),
                r.IsDBNull(14) ? ArchivoEstados.PendienteValidacion : r.GetString(14),
                r.IsDBNull(15) ? null : r.GetDateTime(15),
                r.IsDBNull(16) ? null : r.GetDateTime(16)));
        }
        return list;
    }

    public async Task<ArchivoFullRow?> GetAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
SELECT a.Id, a.DependenciaId, a.LineaTematicaId, a.IndicadorId,
       a.NombreOriginal, a.NombreAlmacenado, a.RutaRelativa, a.TipoMime, a.TamanoBytes,
       a.CreadoEn, a.Observaciones, lt.Nombre, ind.Nombre, u.NombreUsuario, a.SubidoPorUsuarioId,
       a.Estado, a.FechaValidacion, a.FechaEnvio, a.ErroresValidacionJson
FROM dbo.Archivos a
LEFT JOIN dbo.LineaTematica lt ON lt.Id = a.LineaTematicaId
LEFT JOIN dbo.Indicador ind ON ind.Id = a.IndicadorId
LEFT JOIN dbo.Usuarios u ON u.Id = a.SubidoPorUsuarioId
WHERE a.Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return new ArchivoFullRow(
            r.GetInt32(0), r.GetInt32(1),
            r.IsDBNull(2) ? null : r.GetInt32(2),
            r.IsDBNull(3) ? null : r.GetInt32(3),
            r.GetString(4), r.GetString(5), r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.IsDBNull(8) ? null : r.GetInt64(8),
            r.GetDateTime(9),
            r.IsDBNull(10) ? null : r.GetString(10),
            r.IsDBNull(11) ? null : r.GetString(11),
            r.IsDBNull(12) ? null : r.GetString(12),
            r.IsDBNull(13) ? null : r.GetString(13),
            r.IsDBNull(14) ? null : r.GetInt32(14),
            r.IsDBNull(15) ? ArchivoEstados.PendienteValidacion : r.GetString(15),
            r.IsDBNull(16) ? null : r.GetDateTime(16),
            r.IsDBNull(17) ? null : r.GetDateTime(17),
            r.IsDBNull(18) ? null : r.GetString(18));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.Archivos WHERE Id = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}

public sealed record ArchivoInsert(
    int DependenciaId,
    int LineaTematicaId,
    int IndicadorId,
    string NombreOriginal,
    string NombreAlmacenado,
    string RutaRelativa,
    string? TipoMime,
    long? TamanoBytes,
    int? SubidoPorUsuarioId,
    string? Observaciones,
    string? Estado = null);

public sealed record ArchivoEstadoRow(
    int Id,
    string Estado,
    int? SubidoPorUsuarioId,
    int DependenciaId,
    string RutaRelativa,
    string NombreOriginal,
    int? LineaTematicaId,
    int? IndicadorId,
    string? Observaciones);

public sealed record ArchivoListaRow(
    int Id,
    int DependenciaId,
    string DependenciaNombre,
    int? LineaTematicaId,
    string? LineaTematicaNombre,
    int? IndicadorId,
    string? IndicadorNombre,
    string NombreOriginal,
    string? TipoMime,
    long? TamanoBytes,
    DateTime CreadoEn,
    string? SubidoPor,
    string? Observaciones,
    string RutaRelativa,
    string Estado,
    DateTime? FechaValidacion,
    DateTime? FechaEnvio);

public sealed record ArchivoFullRow(
    int Id,
    int DependenciaId,
    int? LineaTematicaId,
    int? IndicadorId,
    string NombreOriginal,
    string NombreAlmacenado,
    string RutaRelativa,
    string? TipoMime,
    long? TamanoBytes,
    DateTime CreadoEn,
    string? Observaciones,
    string? LineaTematicaNombre,
    string? IndicadorNombre,
    string? SubidoPor,
    int? SubidoPorUsuarioId,
    string Estado,
    DateTime? FechaValidacion,
    DateTime? FechaEnvio,
    string? ErroresValidacionJson);
