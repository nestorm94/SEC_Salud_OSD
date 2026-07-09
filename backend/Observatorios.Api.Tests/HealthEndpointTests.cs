using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Observatorios.Api;

namespace Observatorios.Api.Tests;

/// <summary>
/// Prueba de integración del endpoint /health con la API en memoria (sin bootstrap ni seeds).
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Configura el host de prueba con entorno Testing y cadena de conexión mínima.
    /// </summary>
    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Observatorio:SkipSchemaBootstrap"] = "true",
                    ["Observatorio:SkipStartupSeeds"] = "true",
                    ["ConnectionStrings:Default"] =
                        "Server=127.0.0.1,1433;Database=ObservatorioDB_Test;TrustServerCertificate=True;Connection Timeout=1"
                });
            });
        }).CreateClient();
    }

    /// <summary>
    /// Verifica que GET /health responda 200 y contenga el texto Healthy.
    /// </summary>
    [Fact]
    public async Task Health_live_responde_200()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }
}
