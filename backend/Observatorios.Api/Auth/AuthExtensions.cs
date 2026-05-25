using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Observatorios.Api.Auth;

public static class AuthExtensions
{
    public const string PolicyAdmin = "Administrador";

    public static IServiceCollection AddObservatorioAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
            jwt.Key = "ObservatorioSaludCasanare_DevKey_Min32Chars!!";

        services.Configure<JwtSettings>(o =>
        {
            o.Key = jwt.Key;
            o.Issuer = jwt.Issuer;
            o.Audience = jwt.Audience;
            o.ExpireMinutes = jwt.ExpireMinutes;
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyAdmin, p =>
                p.RequireRole("ADMIN", "Administrador"));
        });

        return services;
    }

    public static UserContext? GetUserContext(this HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated != true) return null;
        var sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(sub, out var uid)) return null;

        var depClaim = http.User.FindFirstValue("dependencia_id");
        int? depId = int.TryParse(depClaim, out var d) ? d : null;

        var lineaClaim = http.User.FindFirstValue("linea_tematica_id");
        int? lineaId = int.TryParse(lineaClaim, out var lt) ? lt : null;

        var roles = http.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var user = http.User.FindFirstValue(ClaimTypes.Name)
            ?? http.User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? "";

        var areas = new List<int>();
        var areasClaim = http.User.FindFirstValue("areas");
        if (!string.IsNullOrWhiteSpace(areasClaim))
        {
            foreach (var part in areasClaim.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var aid)) areas.Add(aid);
        }

        return new UserContext
        {
            UsuarioId = uid,
            NombreUsuario = user,
            DependenciaId = depId,
            LineaTematicaId = lineaId,
            Roles = roles,
            AreasTematicasIds = areas
        };
    }
}
