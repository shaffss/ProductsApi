using System.Data.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Products.Persistence;

// Wyjątek (brak połączenia) zamienia na Unhealthy sam mechanizm health checków
internal sealed class DatabaseHealthCheck(DbDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand("SELECT 1");
        await command.ExecuteScalarAsync(ct);
        return HealthCheckResult.Healthy();
    }
}
