namespace Products.Infrastructure.Messaging;

// Sekcja "Kafka" konfiguracji; Enabled = false wyłącza wysyłkę outboxa i konsumenta (np. w benchmarkach)
public sealed class KafkaOptions
{
    public const string Section = "Kafka";

    public bool Enabled { get; set; } = true;
    public string BootstrapServers { get; set; } = "";
    public string ConsumerGroup { get; set; } = "products-api";
    public int TopicPartitions { get; set; } = 3;
    public short TopicReplicationFactor { get; set; } = 1;
    public int OutboxBatchSize { get; set; } = 100;
    public TimeSpan OutboxPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    // wysyłki z błędem samej wiadomości (np. za duża); po limicie wiadomość zostaje w outboxie jako dead-letter
    public int OutboxMaxAttempts { get; set; } = 10;

    // obsługa jednego zdarzenia; po limicie zdarzenie jest pomijane (log Error), żeby nie blokować partycji
    public int ConsumerMaxAttempts { get; set; } = 5;
}
