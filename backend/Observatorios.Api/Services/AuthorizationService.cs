using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

public sealed class AuthorizationService(UsuariosRepository usuarios)
{
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

    public static bool PuedeSubirCargue(UserContext user) =>
        user.EsAdministrador
        || user.TieneRol(RolNombres.ResponsableTematico)
        || user.TieneRol(RolNombres.CoordinadorDependencia);

    public static bool PuedeValidarCargue(UserContext user) =>
        user.EsAdministrador || user.EsValidador;

    public static bool SoloLectura(UserContext user) =>
        user.EsConsulta && !PuedeSubirCargue(user) && !PuedeValidarCargue(user);
}
