using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Products.Core.Abstractions;
using Products.Infrastructure.Caching;
using Products.Infrastructure.Messaging;
using StackExchange.Redis;

namespace Products.Infrastructure;

public static class ServiceCollectionExtensions
{
    // Limit czasu health checków zależności opcjonalnych - zawieszony Redis/Kafka nie wydłuża odpowiedzi /health
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(3);

    public static IServiceCollection AddProductCache(
        this IServiceCollection services, CacheOptions options, string? redisConnectionString)
    {
        if (!options.Enabled)
            return services.AddSingleton<IProductCache, NullProductCache>();

        ArgumentException.ThrowIfNullOrEmpty(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            configuration.AbortOnConnectFail = false; // aplikacja startuje bez Redisa, połączenie nawiązywane w tle
            // bez połączenia polecenie od razu kończy się błędem (odczyt idzie do bazy) zamiast czekać w kolejce do 5 s
            configuration.BacklogPolicy = BacklogPolicy.FailFast;
            configuration.LoggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return ConnectionMultiplexer.Connect(configuration);
        });

        services.AddSingleton(options);
        services.AddSingleton<IProductCache, RedisProductCache>();
        // bez cache aplikacja działa (wolniej) - stan Degraded, nie Unhealthy
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, timeout: HealthCheckTimeout);
        return services;
    }

    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services, KafkaOptions options)
    {
        if (!options.Enabled)
            return services; // outbox zapisywany dalej, ale nie wysyłany

        ArgumentException.ThrowIfNullOrEmpty(options.BootstrapServers);
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            EnableIdempotence = true, // ponowienia bez duplikatów i z zachowaniem kolejności w partycji
            MessageTimeoutMs = 10_000, // niedostępna Kafka: szybki błąd i ponowienie z outboxa zamiast 5 min czekania
        };
        services.AddSingleton(provider => new ProducerBuilder<string, string>(producerConfig)
            .SetLogHandler((_, message) => KafkaLog.Write(provider.GetRequiredService<ILogger<OutboxDispatcher>>(), message))
            .Build());

        services.AddSingleton(provider => new AdminClientBuilder(new AdminClientConfig { BootstrapServers = options.BootstrapServers })
            .SetLogHandler((_, message) => KafkaLog.Write(provider.GetRequiredService<ILogger<KafkaHealthCheck>>(), message))
            .Build());

        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<PriceChangedConsumer>();
        // bez Kafki API działa, zdarzenia czekają w outboxie - stan Degraded, nie Unhealthy
        services.AddHealthChecks().AddCheck<KafkaHealthCheck>("kafka", HealthStatus.Degraded, timeout: HealthCheckTimeout);
        return services;
    }
}
