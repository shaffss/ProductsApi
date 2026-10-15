using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Products.Infrastructure.Caching;

// Wyjątek (brak połączenia, przekroczony czas) zamienia na stan z rejestracji (Degraded) sam mechanizm health checków
internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        await redis.GetDatabase().PingAsync().WaitAsync(ct); // PING nie przyjmuje tokenu - limit czasu z rejestracji
        return HealthCheckResult.Healthy();
    }
}
