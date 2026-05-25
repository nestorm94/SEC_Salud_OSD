using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class AuditoriaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    public async Task RegistrarAsync(int? usuarioId, string accion, string? entidad, string? entidadId, string? detalle, string? ip, CancellationToken ct = default)
    {
        if (!await TablaExisteAsync(ct)) return;
        const string sql = """
INSERT INTO dbo.AuditoriaSistema (UsuarioId, Accion, Entidad, EntidadId, Detalle, IpOrigen)
VALUES (@Uid, @Accion, @Ent, @EntId, @Det, @Ip);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Uid", (object?)usuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Accion", accion);
        cmd.Parameters.AddWithValue("@Ent", (object?)entidad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EntId", (object?)entidadId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Det", (object?)detalle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Ip", (object?)ip ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AuditoriaRow>> ListarAsync(int top = 200, CancellationToken ct = default)
    {
        if (!await TablaExisteAsync(ct)) return [];
        var sql = $"""
SELECT TOP ({top}) a.Id, a.Fecha, u.NombreUsuario, a.Accion, a.Entidad, a.EntidadId, a.Detalle
FROM dbo.AuditoriaSistema a
LEFT JOIN dbo.Usuarios u ON u.Id = a.UsuarioId
ORDER BY a.Fecha DESC;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AuditoriaRow>();
        while (await r.ReadAsync(ct))
            list.Add(new AuditoriaRow(r.GetInt64(0), r.GetDateTime(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6)));
        return list;
    }

    private async Task<bool> TablaExisteAsync(CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(N'dbo.AuditoriaSistema', N'U')", con);
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }
}

public sealed record AuditoriaRow(long Id, DateTime Fecha, string? Usuario, string Accion, string? Entidad, string? EntidadId, string? Detalle);
