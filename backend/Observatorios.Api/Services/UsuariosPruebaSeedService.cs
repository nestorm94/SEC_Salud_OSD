using Microsoft.Data.SqlClient;
using Observatorios.Api.Data;

namespace Observatorios.Api.Services;

/// <summary>
/// Usuarios de prueba (uno por línea temática) para validar cargas e indicadores.
/// Contraseña común: Prueba123*
/// </summary>
public sealed class UsuariosPruebaSeedService(IConfiguration config, UsuariosRepository usuarios)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public const string PasswordPrueba = "Prueba123*";

    private static readonly (string CodigoLinea, string SufijoUsuario, string SufijoEmail)[] Cuentas =
    [
        ("LT-ASEG", "aseg", "aseguramiento"),
        ("LT-ECNT", "ecnt", "ecnt"),
        ("LT-VSP", "vsp", "vigilancia"),
        ("LT-ETC", "etc", "transmisibles"),
        ("LT-ECON", "econ", "economia"),
    ];

    public async Task<int> EnsureSeedAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        var depId = await ObtenerDependenciaCasanareAsync(con, ct);
        var creados = 0;

        foreach (var (codigoLinea, sufijo, emailPart) in Cuentas)
        {
            var lineaId = await ObtenerLineaIdAsync(con, codigoLinea, ct);
            if (lineaId is null) continue;

            var usuario = $"prueba.{sufijo}";
            var email = $"prueba.{emailPart}@observatorio.gov.co";

            var existente = await usuarios.GetByNombreUsuarioAsync(usuario, ct);
            if (existente is not null)
            {
                await AsegurarCuentaPruebaAsync(con, usuario, lineaId.Value, ct);
                continue;
            }

            await usuarios.CrearAsync(new CrearUsuarioRequest(
                usuario,
                PasswordPrueba,
                email,
                depId,
                lineaId,
                ["RESPONSABLE_TEMATICO"]), ct);
            creados++;
        }

        return creados;
    }

    private static async Task<int?> ObtenerDependenciaCasanareAsync(SqlConnection con, CancellationToken ct)
    {
        const string sql = "SELECT TOP 1 Id FROM dbo.Dependencias WHERE Activo = 1 ORDER BY Id;";
        return (int?)await new SqlCommand(sql, con).ExecuteScalarAsync(ct);
    }

    private static async Task<int?> ObtenerLineaIdAsync(SqlConnection con, string codigo, CancellationToken ct)
    {
        const string sql = "SELECT Id FROM dbo.LineaTematica WHERE Codigo = @C AND Activo = 1;";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@C", codigo);
        return (int?)await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task AsegurarCuentaPruebaAsync(SqlConnection con, string nombreUsuario, int lineaId, CancellationToken ct)
    {
        const string sql = """
UPDATE dbo.Usuarios SET LineaTematicaId = @LineaId, Activo = 1 WHERE NombreUsuario = @User;
INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
SELECT u.Id, r.Id
FROM dbo.Usuarios u
CROSS JOIN dbo.Roles r
WHERE u.NombreUsuario = @User AND r.Nombre = N'RESPONSABLE_TEMATICO'
  AND NOT EXISTS (
    SELECT 1 FROM dbo.UsuarioRol ur
    WHERE ur.UsuarioId = u.Id AND ur.RolId = r.Id);
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@User", nombreUsuario);
        cmd.Parameters.AddWithValue("@LineaId", lineaId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
