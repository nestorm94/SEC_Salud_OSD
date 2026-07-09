using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Reglas de autorización de negocio del OSD: quién puede subir, validar o solo consultar cargas.
/// </summary>
public sealed class AuthorizationService(UsuariosRepository usuarios)
{
    /// <summary>Reconstruye UserContext desde BD para un id de usuario.</summary>
    public async Task<UserContext?> BuildContextAsync(int usuarioId, CancellationToken ct = default)
    {
        var u = await usuarios.GetByIdAsync(usuarioId, ct);
        if (u is null || !u.Activo) return null;
        var areas = await usuarios.GetAreasTematicasIdsAsync(usuarioId, ct);
        return new UserContext
        {
            UsuarioId = u.Id,
            NombreUsuario = u.NombreUsuario,
            Email = u.Email,
            DependenciaId = u.DependenciaId,
            Roles = u.Roles,
            AreasTematicasIds = areas
        };
    }

    /// <summary>Indica si el usuario puede iniciar una carga Excel.</summary>
    public static bool PuedeSubirCargue(UserContext user) =>
        user.EsAdministrador
        || user.TieneRol(RolNombres.ResponsableTematico)
        || user.TieneRol(RolNombres.CoordinadorDependencia);

    /// <summary>Indica si el usuario puede aprobar o rechazar cargas validadas.</summary>
    public static bool PuedeValidarCargue(UserContext user) =>
        user.EsAdministrador || user.EsValidador;

    /// <summary>Indica perfil de solo lectura sin permisos de carga ni validación.</summary>
    public static bool SoloLectura(UserContext user) =>
        user.EsConsulta && !PuedeSubirCargue(user) && !PuedeValidarCargue(user);
}
