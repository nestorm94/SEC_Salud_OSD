using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Services;

namespace Observatorios.Api.Data;

/// <summary>Catálogo dim para validación Excel (reutiliza <c>usp_Catalogo_*</c> de fase 4).</summary>
public sealed class CatalogoRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<CatalogoValidacionContext> CargarContextoAsync(CancellationToken ct = default)
    {
        var ctx = new CatalogoValidacionContext();
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        await using (var cmdDep = Sp(con, "dbo.usp_Catalogo_Departamentos_Listar"))
        await using (var rDep = await cmdDep.ExecuteReaderAsync(ct))
        {
            while (await rDep.ReadAsync(ct))
            {
                var cod = rDep.GetString(0);
                ctx.Departamentos[cod] = cod;
            }
        }

        await using (var cmdMun = Sp(con, "dbo.usp_Catalogo_Municipios_Listar"))
        {
            cmdMun.Parameters.AddWithValue("@CodigoDepartamento", DBNull.Value);
            await using var rMun = await cmdMun.ExecuteReaderAsync(ct);
            while (await rMun.ReadAsync(ct))
                ctx.Municipios[rMun.GetString(0)] = rMun.GetString(1);
        }

        return ctx;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
}
