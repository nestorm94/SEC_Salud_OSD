using Observatorios.Api.Models;

namespace Observatorios.Api.Auth;

/// <summary>
/// Representa al usuario autenticado en una petición del OSD, con roles y ámbitos
/// de dependencia, línea temática y áreas para control de acceso.
/// </summary>
public sealed class UserContext
{
    /// <summary>Identificador interno del usuario en dbo.Usuarios.</summary>
    public int UsuarioId { get; init; }
    public string NombreUsuario { get; init; } = "";
    public string? Email { get; init; }
    public int? DependenciaId { get; init; }
    public int? LineaTematicaId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<int> AreasTematicasIds { get; init; } = [];

    /// <summary>Indica si el usuario tiene rol administrador del OSD.</summary>
    public bool EsAdministrador => RolNombres.EsAdmin(Roles);
    /// <summary>Indica si el usuario tiene rol de auditor del sistema.</summary>
    public bool EsAuditor => TieneRol(RolNombres.Auditor);
    /// <summary>Indica si el usuario puede aprobar o rechazar cargas.</summary>
    public bool EsValidador => TieneRol(RolNombres.Validador);
    /// <summary>Indica perfil de solo consulta sin permisos de carga ni validación.</summary>
    public bool EsConsulta => TieneRol(RolNombres.Consulta) && !EsAdministrador;

    /// <summary>Comprueba si el usuario tiene el rol indicado (sin distinguir mayúsculas).</summary>
    public bool TieneRol(string rol) =>
        Roles.Contains(rol, StringComparer.OrdinalIgnoreCase);

    /// <summary>Autoriza acceso a datos de una dependencia según rol y asignación.</summary>
    public bool PuedeAccederDependencia(int dependenciaId) =>
        EsAdministrador || EsAuditor || DependenciaId == dependenciaId;

    /// <summary>Autoriza acceso a un área temática dentro de una dependencia.</summary>
    public bool PuedeAccederArea(int areaTematicaId, int dependenciaId) =>
        EsAdministrador || EsAuditor
        || (PuedeAccederDependencia(dependenciaId) && TieneRol(RolNombres.CoordinadorDependencia))
        || AreasTematicasIds.Contains(areaTematicaId);

    /// <summary>Autoriza operaciones sobre una línea temática (carga, consulta de indicadores).</summary>
    public bool PuedeAccederLineaTematica(int lineaTematicaId) =>
        EsAdministrador || EsValidador || EsAuditor || LineaTematicaId == lineaTematicaId;

    /// <summary>Administrador y validador ven todos los archivos cargados.</summary>
    public bool PuedeVerTodosLosArchivos => EsAdministrador || EsValidador;

    /// <summary>Autoriza ver o descargar un archivo según quién lo subió y su dependencia.</summary>
    public bool PuedeAccederArchivo(int? subidoPorUsuarioId, int dependenciaId) =>
        PuedeVerTodosLosArchivos || UsuarioId == subidoPorUsuarioId;

    /// <summary>Indica si puede gestionar usuarios, roles y configuración del observatorio.</summary>
    public bool PuedeAdministrar => EsAdministrador;
}
