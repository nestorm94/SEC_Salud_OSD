using Microsoft.Data.SqlClient;
using Observatorios.Api.Services;

namespace Observatorios.Api.Data;

public sealed class CatalogoRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<CatalogoValidacionContext> CargarContextoAsync(CancellationToken ct = default)
    {
        var ctx = new CatalogoValidacionContext();
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        if (!await TablaExisteAsync(con, "dim_departamentos", ct)) return ctx;

        await using (var cmd = new SqlCommand("SELECT codigo_departamento, nombre_departamento FROM dbo.dim_departamentos", con))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                ctx.Departamentos[r.GetString(0)] = r.GetString(0);

        if (await TablaExisteAsync(con, "dim_municipios", ct))
        {
            await using var cmd2 = new SqlCommand("SELECT codigo_municipio, codigo_departamento FROM dbo.dim_municipios", con);
            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
                ctx.Municipios[r2.GetString(0)] = r2.GetString(1);
        }
        return ctx;
    }

    private static async Task<bool> TablaExisteAsync(SqlConnection con, string tabla, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@t)", con);
        cmd.Parameters.AddWithValue("@t", $"dbo.{tabla}");
        return (await cmd.ExecuteScalarAsync(ct)) is not null;
    }
}
