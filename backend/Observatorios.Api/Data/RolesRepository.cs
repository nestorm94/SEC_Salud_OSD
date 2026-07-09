using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>
/// Acceso a roles del sistema OSD (dbo.Roles) mediante procedimientos almacenados
/// para administración de permisos y asignación a usuarios.
/// </summary>
public sealed class RolesRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    /// <summary>Lista todos los roles definidos en el observatorio.</summary>
    public async Task<IReadOnlyList<RolRow>> ListarAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        /* SP usp_Roles_Listar */
        await using var cmd = new SqlCommand("dbo.usp_Roles_Listar", con) { CommandType = CommandType.StoredProcedure };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RolRow>();
        while (await r.ReadAsync(ct))
            list.Add(new RolRow(r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
        return list;
    }

    /// <summary>Obtiene un rol por su identificador.</summary>
    /// <param name="id">Id del rol en dbo.Roles.</param>
    public async Task<RolRow?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        /* SP usp_Roles_Obtener */
        await using var cmd = new SqlCommand("dbo.usp_Roles_Obtener", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new RolRow(r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
    }

    /// <summary>Crea un nuevo rol en el catálogo de seguridad.</summary>
    /// <returns>Id del rol creado.</returns>
    public async Task<int> CrearAsync(string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        /* SP usp_Roles_Crear */
        await using var cmd = new SqlCommand("dbo.usp_Roles_Crear", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion ?? DBNull.Value);
        var outId = new SqlParameter("@NuevoId", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(outId);
        await cmd.ExecuteNonQueryAsync(ct);
        return (int)outId.Value!;
    }

    /// <summary>Actualiza nombre y descripción de un rol existente.</summary>
    public async Task ActualizarAsync(int id, string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        /* SP usp_Roles_Actualizar */
        await using var cmd = new SqlCommand("dbo.usp_Roles_Actualizar", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Elimina un rol si no está asignado a usuarios activos.</summary>
    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        /* SP usp_Roles_Eliminar */
        await using var cmd = new SqlCommand("dbo.usp_Roles_Eliminar", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Rol de seguridad del OSD con nombre y descripción.</summary>
public sealed record RolRow(int Id, string Nombre, string? Descripcion);
