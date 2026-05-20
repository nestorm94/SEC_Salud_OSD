using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>Metadata de archivos físicos por dependencia.</summary>
public sealed class ArchivosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<int> InsertAsync(ArchivoInsert row, CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.Archivos
    (DependenciaId, NombreOriginal, NombreAlmacenado, RutaRelativa, TipoMime, TamanoBytes, SubidoPorUsuarioId)
OUTPUT INSERTED.Id
VALUES (@DepId, @NombreOriginal, @NombreAlmacenado, @Ruta, @Mime, @Tam, @UserId);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@DepId", row.DependenciaId);
        cmd.Parameters.AddWithValue("@NombreOriginal", row.NombreOriginal);
        cmd.Parameters.AddWithValue("@NombreAlmacenado", row.NombreAlmacenado);
        cmd.Parameters.AddWithValue("@Ruta", row.RutaRelativa);
        cmd.Parameters.AddWithValue("@Mime", (object?)row.TipoMime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Tam", (object?)row.TamanoBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UserId", (object?)row.SubidoPorUsuarioId ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<ArchivoListaRow>> ListByDependenciaAsync(
        int? dependenciaIdFiltro,
        CancellationToken ct = default)
    {
        var sql = """
SELECT TOP (300) a.Id, a.DependenciaId, d.Nombre AS DependenciaNombre,
       a.NombreOriginal, a.TipoMime, a.TamanoBytes, a.CreadoEn, u.NombreUsuario
FROM dbo.Archivos a
INNER JOIN dbo.Dependencias d ON d.Id = a.DependenciaId
LEFT JOIN dbo.Usuarios u ON u.Id = a.SubidoPorUsuarioId
""";
        if (dependenciaIdFiltro.HasValue)
            sql += " WHERE a.DependenciaId = @DepId";
        sql += " ORDER BY a.Id DESC;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (dependenciaIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ArchivoListaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new ArchivoListaRow(
                r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.GetDateTime(6),
                r.IsDBNull(7) ? null : r.GetString(7)));
        }
        return list;
    }

    public async Task<ArchivoFullRow?> GetAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
SELECT Id, DependenciaId, NombreOriginal, NombreAlmacenado, RutaRelativa, TipoMime, TamanoBytes, CreadoEn
FROM dbo.Archivos WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return new ArchivoFullRow(
            r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3),
            r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetInt64(6), r.GetDateTime(7));
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
    string NombreOriginal,
    string NombreAlmacenado,
    string RutaRelativa,
    string? TipoMime,
    long? TamanoBytes,
    int? SubidoPorUsuarioId);

public sealed record ArchivoListaRow(
    int Id,
    int DependenciaId,
    string DependenciaNombre,
    string NombreOriginal,
    string? TipoMime,
    long? TamanoBytes,
    DateTime CreadoEn,
    string? SubidoPor);

public sealed record ArchivoFullRow(
    int Id,
    int DependenciaId,
    string NombreOriginal,
    string NombreAlmacenado,
    string RutaRelativa,
    string? TipoMime,
    long? TamanoBytes,
    DateTime CreadoEn);
