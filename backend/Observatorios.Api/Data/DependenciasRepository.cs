using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class DependenciasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<int> CrearAsync(string codigo, string nombre, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Dependencia_Crear");
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<DependenciaRow>> ListarAsync(bool soloActivas = true, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Dependencia_Listar");
        cmd.Parameters.AddWithValue("@SoloActivas", soloActivas);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerDependenciasAsync(r, ct);
    }

    public async Task<int> ObtenerOCrearPorCodigoAsync(string codigo, string nombre, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Dependencia_ObtenerOCrear");
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<DependenciaRow?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Dependencia_ObtenerPorId");
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerDependencia(r) : null;
    }

    private static async Task<List<DependenciaRow>> LeerDependenciasAsync(SqlDataReader r, CancellationToken ct)
    {
        var list = new List<DependenciaRow>();
        while (await r.ReadAsync(ct))
            list.Add(LeerDependencia(r));
        return list;
    }

    private static DependenciaRow LeerDependencia(SqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetDateTime(4));

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };
}

public sealed record DependenciaRow(int Id, string Codigo, string Nombre, bool Activo, DateTime CreadoEn);
