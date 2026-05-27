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
}

public sealed record RolRow(int Id, string Nombre, string? Descripcion);
