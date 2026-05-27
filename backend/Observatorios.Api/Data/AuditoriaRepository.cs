using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class AuditoriaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    public async Task RegistrarAsync(
        int? usuarioId, string accion, string? entidad, string? entidadId, string? detalle, string? ip,
        CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Auditoria_Registrar");
        cmd.Parameters.AddWithValue("@UsuarioId", (object?)usuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Accion", accion);
        cmd.Parameters.AddWithValue("@Entidad", (object?)entidad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EntidadId", (object?)entidadId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IpOrigen", (object?)ip ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AuditoriaRow>> ListarAsync(int top = 200, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Auditoria_Listar");
        cmd.Parameters.AddWithValue("@Top", top);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AuditoriaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new AuditoriaRow(
                r.GetInt64(0), r.GetDateTime(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
        }
        return list;
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

public sealed record AuditoriaRow(
    long Id, DateTime Fecha, string? Usuario, string Accion,
    string? Entidad, string? EntidadId, string? Detalle);
