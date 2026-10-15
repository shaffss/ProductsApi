using Testcontainers.Kafka;
using Testcontainers.Redis;

namespace Products.IntegrationTests.Fixtures;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:8.10.2").Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

public sealed class KafkaFixture : IAsyncLifetime
{
    // mały heap JVM (domyślnie 1 GB) - do testów wystarczy, zostawia pamięć dla baz
    private readonly KafkaContainer _container = new KafkaBuilder("apache/kafka:4.3.1")
        .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx256m -Xms256m")
        .Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
