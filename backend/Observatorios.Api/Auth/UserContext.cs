using Observatorios.Api.Models;

namespace Observatorios.Api.Auth;

public sealed class UserContext
{
    public int UsuarioId { get; init; }
    public string NombreUsuario { get; init; } = "";
    public string? Email { get; init; }
    public int? DependenciaId { get; init; }
    public int? LineaTematicaId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<int> AreasTematicasIds { get; init; } = [];

    public bool EsAdministrador => RolNombres.EsAdmin(Roles);
    public bool EsAuditor => TieneRol(RolNombres.Auditor);
    public bool EsValidador => TieneRol(RolNombres.Validador);
    public bool EsConsulta => TieneRol(RolNombres.Consulta) && !EsAdministrador;

    public bool TieneRol(string rol) =>
        Roles.Contains(rol, StringComparer.OrdinalIgnoreCase);

    public bool PuedeAccederDependencia(int dependenciaId) =>
        EsAdministrador || EsAuditor || DependenciaId == dependenciaId;

    public bool PuedeAccederArea(int areaTematicaId, int dependenciaId) =>
        EsAdministrador || EsAuditor
        || (PuedeAccederDependencia(dependenciaId) && TieneRol(RolNombres.CoordinadorDependencia))
        || AreasTematicasIds.Contains(areaTematicaId);

    public bool PuedeAccederLineaTematica(int lineaTematicaId) =>
        EsAdministrador || EsValidador || EsAuditor || LineaTematicaId == lineaTematicaId;

    /// <summary>Administrador y validador ven todos los archivos cargados.</summary>
    public bool PuedeVerTodosLosArchivos => EsAdministrador || EsValidador;

    public bool PuedeAccederArchivo(int? subidoPorUsuarioId, int dependenciaId) =>
        PuedeVerTodosLosArchivos || UsuarioId == subidoPorUsuarioId;

    public bool PuedeAdministrar => EsAdministrador;
}
