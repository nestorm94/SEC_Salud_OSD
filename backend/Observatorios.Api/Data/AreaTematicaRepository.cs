using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class AreaTematicaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    public async Task<IReadOnlyList<AreaTematicaRow>> ListarAsync(int? dependenciaId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_AreaTematica_Listar");
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaId ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AreaTematicaRow>();
        while (await r.ReadAsync(ct))
            list.Add(new AreaTematicaRow(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetBoolean(5)));
        return list;
    }

    public async Task<int> CrearAsync(int dependenciaId, string codigo, string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_AreaTematica_Crear");
        cmd.Parameters.AddWithValue("@DependenciaId", dependenciaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
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

public sealed record AreaTematicaRow(int Id, int DependenciaId, string DependenciaNombre, string Codigo, string Nombre, bool Activo);
