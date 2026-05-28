using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class RolesRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<IReadOnlyList<RolRow>> ListarAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_Roles_Listar", con) { CommandType = CommandType.StoredProcedure };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RolRow>();
        while (await r.ReadAsync(ct))
            list.Add(new RolRow(r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
        return list;
    }

    public async Task<RolRow?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_Roles_Obtener", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new RolRow(r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
    }

    public async Task<int> CrearAsync(string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_Roles_Crear", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion ?? DBNull.Value);
        var outId = new SqlParameter("@NuevoId", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(outId);
        await cmd.ExecuteNonQueryAsync(ct);
        return (int)outId.Value!;
    }

    public async Task ActualizarAsync(int id, string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_Roles_Actualizar", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_Roles_Eliminar", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed record RolRow(int Id, string Nombre, string? Descripcion);
