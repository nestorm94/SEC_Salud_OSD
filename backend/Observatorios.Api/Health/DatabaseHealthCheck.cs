using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Observatorios.Api.Health;

public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cs = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return HealthCheckResult.Degraded("Sin ConnectionStrings:Default configurada.");

        try
        {
            await using var con = new SqlConnection(cs);
            await con.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy("SQL Server accesible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("No se pudo conectar a SQL Server.", ex);
        }
    }
}
