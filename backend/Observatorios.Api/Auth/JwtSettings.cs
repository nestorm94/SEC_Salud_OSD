namespace Observatorios.Api.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Observatorios.Api";
    public string Audience { get; set; } = "Observatorios.Front";
    public int ExpireMinutes { get; set; } = 480;
}
