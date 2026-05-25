using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class DependenciasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<int> CrearAsync(string codigo, string nombre, CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.Dependencias (Codigo, Nombre)
OUTPUT INSERTED.Id
VALUES (@Codigo, @Nombre);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<DependenciaRow>> ListarAsync(bool soloActivas = true, CancellationToken ct = default)
    {
        var sql = """
SELECT Id, Codigo, Nombre, Activo, CreadoEn
FROM dbo.Dependencias
""" + (soloActivas ? " WHERE Activo = 1" : "") + " ORDER BY Nombre;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DependenciaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new DependenciaRow(
                r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetDateTime(4)));
        }
        return list;
    }

    public async Task<int> ObtenerOCrearPorCodigoAsync(string codigo, string nombre, CancellationToken ct = default)
    {
        var c = codigo.Trim().ToUpperInvariant();
        const string find = "SELECT Id FROM dbo.Dependencias WHERE Codigo = @Codigo;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
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
        const string sql = "SELECT Id, Codigo, Nombre, Activo, CreadoEn FROM dbo.Dependencias WHERE Id = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return new DependenciaRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetDateTime(4));
    }
}

public sealed record DependenciaRow(int Id, string Codigo, string Nombre, bool Activo, DateTime CreadoEn);
