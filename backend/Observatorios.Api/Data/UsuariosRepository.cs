using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>
/// Persistencia de usuarios del OSD: autenticación, roles, dependencias y áreas temáticas
/// asignadas mediante procedimientos dbo.usp_Usuario_*.
/// </summary>
public sealed class UsuariosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    /// <summary>Busca usuario activo por correo electrónico (login alternativo).</summary>
    public async Task<UsuarioDbRow?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ObtenerPorEmail");
        cmd.Parameters.AddWithValue("@Email", email.Trim());
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerUsuarioDb(r) : null;
    }

    /// <summary>Busca usuario por nombre de usuario para autenticación.</summary>
    public async Task<UsuarioDbRow?> GetByNombreUsuarioAsync(string nombreUsuario, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ObtenerPorNombre");
        cmd.Parameters.AddWithValue("@NombreUsuario", nombreUsuario);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerUsuarioDb(r) : null;
    }

    /// <summary>Ids de áreas temáticas asignadas al usuario (alcance de carga).</summary>
    public async Task<IReadOnlyList<int>> GetAreasTematicasIdsAsync(int usuarioId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ObtenerAreasTematicas");
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<int>();
        while (await r.ReadAsync(ct))
            list.Add(r.GetInt32(0));
        return list;
    }

    /// <summary>Nombres de roles del usuario para emisión de JWT y autorización.</summary>
    public async Task<IReadOnlyList<string>> GetRolesAsync(int usuarioId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ObtenerRoles");
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<string>();
        while (await r.ReadAsync(ct))
            list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Crea usuario con hash BCrypt y roles iniciales.</summary>
    /// <returns>Id del usuario creado.</returns>
    public async Task<int> CrearAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_Crear");
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LineaTematicaId", (object?)req.LineaTematicaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NombreUsuario", req.NombreUsuario);
        cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PasswordHash", hash);
        cmd.Parameters.AddWithValue("@RolesCsv", SqlProcHelper.RolesToCsv(req.Roles));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    /// <summary>Lista todos los usuarios con dependencia, línea y roles.</summary>
    public async Task<IReadOnlyList<UsuarioListaRow>> ListarAsync(CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_Listar");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerUsuariosListaAsync(r, ct);
    }

    /// <summary>Obtiene detalle de usuario por id incluyendo roles.</summary>
    public async Task<UsuarioDetalleRow?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ObtenerPorId");
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerUsuarioDetalle(r) : null;
    }

    /// <summary>Actualiza email, dependencia, línea y opcionalmente contraseña.</summary>
    public async Task ActualizarAsync(int id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_Actualizar");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LineaTematicaId", (object?)req.LineaTematicaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PasswordHash",
            !string.IsNullOrWhiteSpace(req.Password)
                ? BCrypt.Net.BCrypt.HashPassword(req.Password!)
                : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Activa o desactiva la cuenta del usuario.</summary>
    public async Task SetActivoAsync(int id, bool activo, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_SetActivo");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Activo", activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Reemplaza la lista completa de roles del usuario.</summary>
    public async Task ActualizarRolesAsync(int usuarioId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ActualizarRoles");
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@RolesCsv", SqlProcHelper.RolesToCsv(roles));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Asigna áreas temáticas sobre las que el usuario puede operar.</summary>
    public async Task ActualizarAreasTematicasAsync(int usuarioId, IReadOnlyList<int> areaIds, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Usuario_ActualizarAreasTematicas");
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@AreaIdsCsv", SqlProcHelper.AreaIdsToCsv(areaIds));
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
        r.GetInt32(0), r.IsDBNull(1) ? null : r.GetInt32(1), r.IsDBNull(2) ? null : r.GetInt32(2),
        r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5), r.GetBoolean(6),
        r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8));
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
    int Id, int? DependenciaId, int? LineaTematicaId,
    string NombreUsuario, string? Email, string PasswordHash, bool Activo,
    string? DependenciaNombre, string? LineaTematicaNombre);

public sealed record CrearUsuarioRequest(
    string NombreUsuario, string Password, string? Email,
    int? DependenciaId, int? LineaTematicaId, IReadOnlyList<string> Roles);
