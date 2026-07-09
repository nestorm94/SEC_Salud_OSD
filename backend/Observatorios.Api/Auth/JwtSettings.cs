namespace Observatorios.Api.Auth;

/// <summary>
/// Parámetros de configuración JWT para autenticación del OSD Casanare.
/// Se leen de la sección <c>Jwt</c> en appsettings.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>Nombre de la sección en IConfiguration.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Clave simétrica para firmar tokens (mínimo 32 caracteres).</summary>
    public string Key { get; set; } = "";
    /// <summary>Emisor del token JWT.</summary>
    public string Issuer { get; set; } = "Observatorios.Api";
    /// <summary>Audiencia esperada del token JWT.</summary>
    public string Audience { get; set; } = "Observatorios.Front";
    /// <summary>Minutos de validez del token desde su emisión.</summary>
    public int ExpireMinutes { get; set; } = 720;
}
