using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Observatorios.Api.Health;

/// <summary>
/// Comprueba la conectividad con SQL Server usando la cadena ConnectionStrings:Default.
/// </summary>
public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    /// <summary>
    /// Intenta abrir una conexión a la base de datos y devuelve Healthy, Degraded o Unhealthy.
    /// </summary>
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
