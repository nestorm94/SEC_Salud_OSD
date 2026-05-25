using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;

namespace Observatorios.Api.Services;

public sealed class AuthService(
    UsuariosRepository usuarios,
    IOptions<JwtSettings> jwtOptions)
{
    public async Task<LoginResult?> LoginAsync(string nombreUsuario, string password, CancellationToken ct)
    {
        var key = nombreUsuario.Trim();
        var user = await usuarios.GetByNombreUsuarioAsync(key, ct)
            ?? (key.Contains('@') ? await usuarios.GetByEmailAsync(key, ct) : null);
        if (user is null || !user.Activo) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

        var roles = await usuarios.GetRolesAsync(user.Id, ct);
        var areas = await usuarios.GetAreasTematicasIdsAsync(user.Id, ct);
        var token = GenerarToken(user.Id, user.NombreUsuario, user.DependenciaId, user.LineaTematicaId, roles, areas);
        return new LoginResult(
            token,
            user.Id,
            user.NombreUsuario,
            user.Email,
            user.DependenciaId,
            user.DependenciaNombre,
            user.LineaTematicaId,
            user.LineaTematicaNombre,
            roles);
    }

    private string GenerarToken(int userId, string nombreUsuario, int? dependenciaId, int? lineaTematicaId, IReadOnlyList<string> roles, IReadOnlyList<int> areas)
    {
        var jwt = jwtOptions.Value;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, nombreUsuario),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, nombreUsuario)
        };
        if (dependenciaId.HasValue)
            claims.Add(new Claim("dependencia_id", dependenciaId.Value.ToString()));
        if (lineaTematicaId.HasValue)
            claims.Add(new Claim("linea_tematica_id", lineaTematicaId.Value.ToString()));

        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));
        if (areas.Count > 0)
            claims.Add(new Claim("areas", string.Join(',', areas)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var exp = DateTime.UtcNow.AddMinutes(jwt.ExpireMinutes);

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: exp,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record LoginResult(
    string Token,
    int UsuarioId,
    string NombreUsuario,
    string? Email,
    int? DependenciaId,
    string? DependenciaNombre,
    int? LineaTematicaId,
    string? LineaTematicaNombre,
    IReadOnlyList<string> Roles);
