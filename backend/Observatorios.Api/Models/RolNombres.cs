namespace Observatorios.Api.Models;

/// <summary>
/// Nombres canónicos de roles del OSD Casanare (v2) para autorización y asignación de permisos.
/// </summary>
public static class RolNombres
{
    /// <summary>Administrador del sistema con acceso total.</summary>
    public const string Admin = "ADMIN";
    /// <summary>Coordina cargas y usuarios de su dependencia.</summary>
    public const string CoordinadorDependencia = "COORDINADOR_DEPENDENCIA";
    /// <summary>Responsable de cargar datos de su línea temática asignada.</summary>
    public const string ResponsableTematico = "RESPONSABLE_TEMATICO";
    /// <summary>Revisa, aprueba o rechaza cargas validadas.</summary>
    public const string Validador = "VALIDADOR";
    /// <summary>Solo consulta indicadores y reportes.</summary>
    public const string Consulta = "CONSULTA";
    /// <summary>Acceso de solo lectura a auditoría y trazabilidad.</summary>
    public const string Auditor = "AUDITOR";

    /// <summary>Determina si la colección incluye rol administrador (v1 o v2).</summary>
    public static bool EsAdmin(IEnumerable<string> roles) =>
        roles.Any(r => r.Equals(Admin, StringComparison.OrdinalIgnoreCase)
                    || r.Equals("Administrador", StringComparison.OrdinalIgnoreCase));
}
