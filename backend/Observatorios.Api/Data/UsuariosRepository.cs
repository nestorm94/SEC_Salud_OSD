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
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ObtenerPorEmail", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ObtenerPorEmail", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Email", email.Trim());
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            return await r.ReadAsync(ct) ? LeerUsuarioDb(r) : null;
        }

        var sql = $"SELECT {SelectUsuarioCols} {FromUsuario} WHERE u.Email = @Email;";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Email", email.Trim());
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await rLegacy.ReadAsync(ct) ? LeerUsuarioDb(rLegacy) : null;
    }

    public async Task<UsuarioDbRow?> GetByNombreUsuarioAsync(string nombreUsuario, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ObtenerPorNombre", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ObtenerPorNombre", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@NombreUsuario", nombreUsuario);
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            return await r.ReadAsync(ct) ? LeerUsuarioDb(r) : null;
        }

        var sql = $"SELECT {SelectUsuarioCols} {FromUsuario} WHERE u.NombreUsuario = @User;";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@User", nombreUsuario);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await rLegacy.ReadAsync(ct) ? LeerUsuarioDb(rLegacy) : null;
    }

    public async Task<IReadOnlyList<int>> GetAreasTematicasIdsAsync(int usuarioId, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ObtenerAreasTematicas", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ObtenerAreasTematicas", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<int>();
            while (await r.ReadAsync(ct))
                list.Add(r.GetInt32(0));
            return list;
        }

        if (!await TablaExisteAsync(con, "UsuarioAreaTematica", ct)) return [];
        const string sql = "SELECT AreaTematicaId FROM dbo.UsuarioAreaTematica WHERE UsuarioId = @Id;";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Id", usuarioId);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(ct);
        var listLegacy = new List<int>();
        while (await rLegacy.ReadAsync(ct)) listLegacy.Add(rLegacy.GetInt32(0));
        return listLegacy;
    }

    private static async Task<bool> TablaExisteAsync(SqlConnection con, string tabla, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@t)", con);
        cmd.Parameters.AddWithValue("@t", $"dbo.{tabla}");
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(int usuarioId, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ObtenerRoles", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ObtenerRoles", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<string>();
            while (await r.ReadAsync(ct)) list.Add(r.GetString(0));
            return list;
        }

        const string sql = """
SELECT r.Nombre FROM dbo.UsuarioRol ur
INNER JOIN dbo.Roles r ON r.Id = ur.RolId
WHERE ur.UsuarioId = @Id;
""";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Id", usuarioId);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(ct);
        var listLegacy = new List<string>();
        while (await rLegacy.ReadAsync(ct)) listLegacy.Add(rLegacy.GetString(0));
        return listLegacy;
    }

    public async Task<int> CrearAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_Crear", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_Crear", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@DependenciaId", (object?)req.DependenciaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LineaTematicaId", (object?)req.LineaTematicaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NombreUsuario", req.NombreUsuario);
            cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PasswordHash", hash);
            cmd.Parameters.AddWithValue("@RolesCsv", SqlProcHelper.RolesToCsv(req.Roles));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);
        const string ins = """
INSERT INTO dbo.Usuarios (DependenciaId, LineaTematicaId, NombreUsuario, Email, PasswordHash, Activo)
OUTPUT INSERTED.Id
VALUES (@DepId, @LineaId, @User, @Email, @Hash, 1);
""";
        await using var cmdIns = new SqlCommand(ins, con, tx);
        cmdIns.Parameters.AddWithValue("@DepId", (object?)req.DependenciaId ?? DBNull.Value);
        cmdIns.Parameters.AddWithValue("@LineaId", (object?)req.LineaTematicaId ?? DBNull.Value);
        cmdIns.Parameters.AddWithValue("@User", req.NombreUsuario);
        cmdIns.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
        cmdIns.Parameters.AddWithValue("@Hash", hash);
        var id = Convert.ToInt32(await cmdIns.ExecuteScalarAsync(ct));

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
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_Listar", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_Listar", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await LeerUsuariosListaAsync(r, ct);
        }

        var sql = """
SELECT u.Id, u.NombreUsuario, u.Email, u.Activo, u.DependenciaId, d.Nombre AS DependenciaNombre,
       u.LineaTematicaId, lt.Nombre AS LineaTematicaNombre
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId
ORDER BY u.NombreUsuario;
""";
        await using var cmdLegacy = new SqlCommand(sql, con);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(ct);
        var list = new List<UsuarioListaRow>();
        while (await rLegacy.ReadAsync(ct))
        {
            list.Add(new UsuarioListaRow(
                rLegacy.GetInt32(0), rLegacy.GetString(1), rLegacy.IsDBNull(2) ? null : rLegacy.GetString(2),
                rLegacy.GetBoolean(3), rLegacy.IsDBNull(4) ? null : rLegacy.GetInt32(4),
                rLegacy.IsDBNull(5) ? null : rLegacy.GetString(5),
                rLegacy.IsDBNull(6) ? null : rLegacy.GetInt32(6),
                rLegacy.IsDBNull(7) ? null : rLegacy.GetString(7), []));
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
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ObtenerPorId", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ObtenerPorId", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (!await r.ReadAsync(ct)) return null;
            return LeerUsuarioDetalle(r);
        }

        var sql = """
SELECT u.Id, u.NombreUsuario, u.Email, u.Activo, u.DependenciaId, d.Nombre,
       u.LineaTematicaId, lt.Nombre AS LineaTematicaNombre
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId
WHERE u.Id = @Id;
""";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Id", id);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await rLegacy.ReadAsync(ct)) return null;
        var uid = rLegacy.GetInt32(0);
        var detalle = new UsuarioDetalleRow(
            uid, rLegacy.GetString(1), rLegacy.IsDBNull(2) ? null : rLegacy.GetString(2), rLegacy.GetBoolean(3),
            rLegacy.IsDBNull(4) ? null : rLegacy.GetInt32(4), rLegacy.IsDBNull(5) ? null : rLegacy.GetString(5),
            rLegacy.IsDBNull(6) ? null : rLegacy.GetInt32(6), rLegacy.IsDBNull(7) ? null : rLegacy.GetString(7), []);
        await rLegacy.DisposeAsync();
        var roles = await GetRolesAsync(uid, ct);
        return detalle with { Roles = roles };
    }

    public async Task ActualizarAsync(int id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_Actualizar", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_Actualizar", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DependenciaId", (object?)req.DependenciaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LineaTematicaId", (object?)req.LineaTematicaId ?? DBNull.Value);
            if (!string.IsNullOrWhiteSpace(req.Password))
                cmd.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(req.Password!));
            else
                cmd.Parameters.AddWithValue("@PasswordHash", DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

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
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Id", id);
        cmdLegacy.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
        cmdLegacy.Parameters.AddWithValue("@DepId", (object?)req.DependenciaId ?? DBNull.Value);
        cmdLegacy.Parameters.AddWithValue("@LineaId", (object?)req.LineaTematicaId ?? DBNull.Value);
        if (!string.IsNullOrWhiteSpace(req.Password))
            cmdLegacy.Parameters.AddWithValue("@Hash", BCrypt.Net.BCrypt.HashPassword(req.Password!));
        await cmdLegacy.ExecuteNonQueryAsync(ct);
    }

    public async Task SetActivoAsync(int id, bool activo, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_SetActivo", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_SetActivo", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Activo", activo);
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        const string sql = "UPDATE dbo.Usuarios SET Activo = @Activo WHERE Id = @Id;";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Id", id);
        cmdLegacy.Parameters.AddWithValue("@Activo", activo);
        await cmdLegacy.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarRolesAsync(int usuarioId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ActualizarRoles", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ActualizarRoles", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
            cmd.Parameters.AddWithValue("@RolesCsv", SqlProcHelper.RolesToCsv(roles));
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

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
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Usuario_ActualizarAreasTematicas", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Usuario_ActualizarAreasTematicas", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
            cmd.Parameters.AddWithValue("@AreaIdsCsv", SqlProcHelper.AreaIdsToCsv(areaIds));
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        if (!await TablaExisteAsync(con, "UsuarioAreaTematica", ct)) return;
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

    private static async Task<List<UsuarioListaRow>> LeerUsuariosListaAsync(SqlDataReader r, CancellationToken ct)
    {
        var list = new List<UsuarioListaRow>();
        while (await r.ReadAsync(ct))
        {
            var rolesCsv = r.IsDBNull(8) ? null : r.GetString(8);
            list.Add(new UsuarioListaRow(
                r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.GetBoolean(3), r.IsDBNull(4) ? null : r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt32(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                SqlProcHelper.RolesFromCsv(rolesCsv)));
        }
        return list;
    }

    private static UsuarioDetalleRow LeerUsuarioDetalle(SqlDataReader r)
    {
        var rolesCsv = r.IsDBNull(8) ? null : r.GetString(8);
        return new UsuarioDetalleRow(
            r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetBoolean(3),
            r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetInt32(6), r.IsDBNull(7) ? null : r.GetString(7),
            SqlProcHelper.RolesFromCsv(rolesCsv));
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
