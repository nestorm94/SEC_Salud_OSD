using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class AreaTematicaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")!;

    public async Task<IReadOnlyList<AreaTematicaRow>> ListarAsync(int? dependenciaId, CancellationToken ct = default)
    {
        var sql = """
SELECT a.Id, a.DependenciaId, d.Nombre, a.Codigo, a.Nombre, a.Activo
FROM dbo.AreaTematica a
INNER JOIN dbo.Dependencias d ON d.Id = a.DependenciaId
WHERE (@DepId IS NULL OR a.DependenciaId = @DepId)
ORDER BY d.Nombre, a.Nombre;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@DepId", (object?)dependenciaId ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AreaTematicaRow>();
        while (await r.ReadAsync(ct))
            list.Add(new AreaTematicaRow(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetBoolean(5)));
        return list;
    }

    public async Task<int> CrearAsync(int dependenciaId, string codigo, string nombre, string? descripcion, CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.AreaTematica (DependenciaId, Codigo, Nombre, Descripcion)
OUTPUT INSERTED.Id VALUES (@DepId, @Codigo, @Nombre, @Desc);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@DepId", dependenciaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("@Desc", (object?)descripcion ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}

public sealed record AreaTematicaRow(int Id, int DependenciaId, string DependenciaNombre, string Codigo, string Nombre, bool Activo);
