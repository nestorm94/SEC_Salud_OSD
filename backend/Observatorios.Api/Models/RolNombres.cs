namespace Observatorios.Api.Models;

/// <summary>Roles del observatorio (v2).</summary>
public static class RolNombres
{
    public const string Admin = "ADMIN";
    public const string CoordinadorDependencia = "COORDINADOR_DEPENDENCIA";
    public const string ResponsableTematico = "RESPONSABLE_TEMATICO";
    public const string Validador = "VALIDADOR";
    public const string Consulta = "CONSULTA";
    public const string Auditor = "AUDITOR";

    public static bool EsAdmin(IEnumerable<string> roles) =>
        roles.Any(r => r.Equals(Admin, StringComparison.OrdinalIgnoreCase)
                    || r.Equals("Administrador", StringComparison.OrdinalIgnoreCase));
}
