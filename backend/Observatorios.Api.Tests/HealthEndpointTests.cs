using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Observatorios.Api;

namespace Observatorios.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

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

    [Fact]
    public async Task Health_live_responde_200()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }
}
