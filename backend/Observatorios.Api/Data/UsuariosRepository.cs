using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class UsuariosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    private const string SelectUsuarioCols = """
u.Id, u.DependenciaId, u.LineaTematicaId, u.NombreUsuario, u.Email, u.PasswordHash, u.Activo,
d.Nombre AS DependenciaNombre, lt.Nombre AS LineaTematicaNombre
""";

    private const string FromUsuario = """
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId
""";

    public async Task<UsuarioDbRow?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectUsuarioCols} {FromUsuario} WHERE u.Email = @Email;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Email", email.Trim());
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerUsuarioDb(r) : null;
    }

    public async Task<UsuarioDbRow?> GetByNombreUsuarioAsync(string nombreUsuario, CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectUsuarioCols} {FromUsuario} WHERE u.NombreUsuario = @User;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@User", nombreUsuario);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerUsuarioDb(r) : null;
    }

    public async Task<IReadOnlyList<int>> GetAreasTematicasIdsAsync(int usuarioId, CancellationToken ct = default)
    {
        if (!await TablaExisteAsync("UsuarioAreaTematica", ct)) return [];
        const string sql = "SELECT AreaTematicaId FROM dbo.UsuarioAreaTematica WHERE UsuarioId = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", usuarioId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<int>();
        while (await r.ReadAsync(ct)) list.Add(r.GetInt32(0));
        return list;
    }

    private async Task<bool> TablaExisteAsync(string tabla, CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@t)", con);
        cmd.Parameters.AddWithValue("@t", $"dbo.{tabla}");
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(int usuarioId, CancellationToken ct = default)
    {
        const string sql = """
SELECT r.Nombre FROM dbo.UsuarioRol ur
INNER JOIN dbo.Roles r ON r.Id = ur.RolId
WHERE ur.UsuarioId = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", usuarioId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<string>();
        while (await r.ReadAsync(ct)) list.Add(r.GetString(0));
        return list;
    }

    public async Task<int> CrearAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

        const string ins = """
INSERT INTO dbo.Usuarios (DependenciaId, LineaTematicaId, NombreUsuario, Email, PasswordHash, Activo)
OUTPUT INSERTED.Id
VALUES (@DepId, @LineaId, @User, @Email, @Hash, 1);
""";
        await using var cmd = new SqlCommand(ins, con, tx);
        cmd.Parameters.AddWithValue("@DepId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LineaId", (object?)req.LineaTematicaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@User", req.NombreUsuario);
        cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Hash", hash);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

        foreach (var rol in req.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            const string rolSql = """
INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
SELECT @Uid, Id FROM dbo.Roles WHERE Nombre = @Rol;
""";
            await using var rc = new SqlCommand(rolSql, con, tx);
            rc.Parameters.AddWithValue("@Uid", id);
            rc.Parameters.AddWithValue("@Rol", rol);
            await rc.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<IReadOnlyList<UsuarioListaRow>> ListarAsync(CancellationToken ct = default)
    {
        var sql = $"""
SELECT u.Id, u.NombreUsuario, u.Email, u.Activo, u.DependenciaId, d.Nombre AS DependenciaNombre,
       u.LineaTematicaId, lt.Nombre AS LineaTematicaNombre
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId
ORDER BY u.NombreUsuario;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UsuarioListaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new UsuarioListaRow(
                r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.GetBoolean(3), r.IsDBNull(4) ? null : r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt32(6),
                r.IsDBNull(7) ? null : r.GetString(7), []));
        }
        for (var i = 0; i < list.Count; i++)
        {
            var roles = await GetRolesAsync(list[i].Id, ct);
            list[i] = list[i] with { Roles = roles };
        }
        return list;
    }

    public async Task<UsuarioDetalleRow?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var sql = $"""
SELECT u.Id, u.NombreUsuario, u.Email, u.Activo, u.DependenciaId, d.Nombre,
       u.LineaTematicaId, lt.Nombre AS LineaTematicaNombre
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId
WHERE u.Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        var uid = r.GetInt32(0);
        var detalle = new UsuarioDetalleRow(
            uid, r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetBoolean(3),
            r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetInt32(6), r.IsDBNull(7) ? null : r.GetString(7), []);
        await r.DisposeAsync();
        var roles = await GetRolesAsync(uid, ct);
        return detalle with { Roles = roles };
    }

    public async Task ActualizarAsync(int id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        var sql = """
UPDATE dbo.Usuarios SET Email = @Email, DependenciaId = @DepId, LineaTematicaId = @LineaId
WHERE Id = @Id;
""";
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            sql = """
UPDATE dbo.Usuarios SET Email = @Email, DependenciaId = @DepId, LineaTematicaId = @LineaId, PasswordHash = @Hash
WHERE Id = @Id;
""";
        }
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DepId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LineaId", (object?)req.LineaTematicaId ?? DBNull.Value);
        if (!string.IsNullOrWhiteSpace(req.Password))
            cmd.Parameters.AddWithValue("@Hash", BCrypt.Net.BCrypt.HashPassword(req.Password!));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetActivoAsync(int id, bool activo, CancellationToken ct = default)
    {
        const string sql = "UPDATE dbo.Usuarios SET Activo = @Activo WHERE Id = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Activo", activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarRolesAsync(int usuarioId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);
        await using (var del = new SqlCommand("DELETE FROM dbo.UsuarioRol WHERE UsuarioId = @Id;", con, tx))
        {
            del.Parameters.AddWithValue("@Id", usuarioId);
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var rol in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            const string ins = """
INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
SELECT @Uid, Id FROM dbo.Roles WHERE Nombre = @Rol;
""";
            await using var cmd = new SqlCommand(ins, con, tx);
            cmd.Parameters.AddWithValue("@Uid", usuarioId);
            cmd.Parameters.AddWithValue("@Rol", rol);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task ActualizarAreasTematicasAsync(int usuarioId, IReadOnlyList<int> areaIds, CancellationToken ct = default)
    {
        if (!await TablaExisteAsync("UsuarioAreaTematica", ct)) return;
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);
        await using (var del = new SqlCommand("DELETE FROM dbo.UsuarioAreaTematica WHERE UsuarioId = @Id;", con, tx))
        {
            del.Parameters.AddWithValue("@Id", usuarioId);
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var aid in areaIds.Distinct())
        {
            await using var ins = new SqlCommand(
                "INSERT INTO dbo.UsuarioAreaTematica (UsuarioId, AreaTematicaId) VALUES (@Uid, @Aid);", con, tx);
            ins.Parameters.AddWithValue("@Uid", usuarioId);
            ins.Parameters.AddWithValue("@Aid", aid);
            await ins.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private static UsuarioDbRow LeerUsuarioDb(SqlDataReader r) => new(
        r.GetInt32(0),
        r.IsDBNull(1) ? null : r.GetInt32(1),
        r.IsDBNull(2) ? null : r.GetInt32(2),
        r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        r.GetString(5),
        r.GetBoolean(6),
        r.IsDBNull(7) ? null : r.GetString(7),
        r.IsDBNull(8) ? null : r.GetString(8));
}

public sealed record UsuarioListaRow(
    int Id, string NombreUsuario, string? Email, bool Activo,
    int? DependenciaId, string? DependenciaNombre,
    int? LineaTematicaId, string? LineaTematicaNombre,
    IReadOnlyList<string> Roles);

public sealed record UsuarioDetalleRow(
    int Id, string NombreUsuario, string? Email, bool Activo,
    int? DependenciaId, string? DependenciaNombre,
    int? LineaTematicaId, string? LineaTematicaNombre,
    IReadOnlyList<string> Roles);

public sealed record ActualizarUsuarioRequest(
    string? Email, int? DependenciaId, int? LineaTematicaId, string? Password);

public sealed record UsuarioDbRow(
    int Id,
    int? DependenciaId,
    int? LineaTematicaId,
    string NombreUsuario,
    string? Email,
    string PasswordHash,
    bool Activo,
    string? DependenciaNombre,
    string? LineaTematicaNombre);

public sealed record CrearUsuarioRequest(
    string NombreUsuario,
    string Password,
    string? Email,
    int? DependenciaId,
    int? LineaTematicaId,
    IReadOnlyList<string> Roles);
