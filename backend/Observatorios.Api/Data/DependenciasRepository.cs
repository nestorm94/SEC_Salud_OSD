using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class DependenciasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<int> CrearAsync(string codigo, string nombre, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Dependencia_Crear", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Dependencia_Crear", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
            cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        const string sql = """
INSERT INTO dbo.Dependencias (Codigo, Nombre)
OUTPUT INSERTED.Id
VALUES (@Codigo, @Nombre);
""";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Codigo", codigo.Trim().ToUpperInvariant());
        cmdLegacy.Parameters.AddWithValue("@Nombre", nombre.Trim());
        return Convert.ToInt32(await cmdLegacy.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<DependenciaRow>> ListarAsync(bool soloActivas = true, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Dependencia_Listar", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Dependencia_Listar", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@SoloActivas", soloActivas);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await LeerDependenciasAsync(r, ct);
        }

        var sql = """
SELECT Id, Codigo, Nombre, Activo, CreadoEn
FROM dbo.Dependencias
""" + (soloActivas ? " WHERE Activo = 1" : "") + " ORDER BY Nombre;";

        await using var cmdLegacy = new SqlCommand(sql, con);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(ct);
        return await LeerDependenciasAsync(rLegacy, ct);
    }

    public async Task<int> ObtenerOCrearPorCodigoAsync(string codigo, string nombre, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Dependencia_ObtenerOCrear", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Dependencia_ObtenerOCrear", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
            cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        var c = codigo.Trim().ToUpperInvariant();
        const string find = "SELECT Id FROM dbo.Dependencias WHERE Codigo = @Codigo;";
        await using (var cmd = new SqlCommand(find, con))
        {
            cmd.Parameters.AddWithValue("@Codigo", c);
            var id = await cmd.ExecuteScalarAsync(ct);
            if (id is not null && id != DBNull.Value)
                return Convert.ToInt32(id);
        }
        return await CrearAsync(c, string.IsNullOrWhiteSpace(nombre) ? c : nombre, ct);
    }

    public async Task<DependenciaRow?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Dependencia_ObtenerPorId", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Dependencia_ObtenerPorId", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (!await r.ReadAsync(ct)) return null;
            return LeerDependencia(r);
        }

        const string sql = "SELECT Id, Codigo, Nombre, Activo, CreadoEn FROM dbo.Dependencias WHERE Id = @Id;";
        await using var cmdLegacy = new SqlCommand(sql, con);
        cmdLegacy.Parameters.AddWithValue("@Id", id);
        await using var rLegacy = await cmdLegacy.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await rLegacy.ReadAsync(ct)) return null;
        return LeerDependencia(rLegacy);
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
}

public sealed record DependenciaRow(int Id, string Codigo, string Nombre, bool Activo, DateTime CreadoEn);
