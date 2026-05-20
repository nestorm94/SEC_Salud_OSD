namespace Observatorios.Api.Auth;

/// <summary>Usuario autenticado extraído del token JWT.</summary>
public sealed class UserContext
{
    public int UsuarioId { get; init; }
    public string NombreUsuario { get; init; } = "";
    public int? DependenciaId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool EsAdministrador =>
        Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);

    public bool PuedeAccederDependencia(int dependenciaId) =>
        EsAdministrador || DependenciaId == dependenciaId;
}
