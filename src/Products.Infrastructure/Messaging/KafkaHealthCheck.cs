using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Products.Infrastructure.Messaging;

// Wyjątek (brak brokera) zamienia na stan z rejestracji (Degraded) sam mechanizm health checków
internal sealed class KafkaHealthCheck(IAdminClient admin) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.Run(() => // GetMetadata blokuje wątek
        {
            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(2));
            return metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "No Kafka brokers available");
        }, ct);
}
