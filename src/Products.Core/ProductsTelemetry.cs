using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Products.Core;

// Ślady i metryki aplikacji (OpenTelemetry przez System.Diagnostics - bez zależności od SDK).
// Bez nasłuchującego eksportera StartActivity zwraca null, a pomiary są pomijane - praktycznie zerowy koszt.
public static class ProductsTelemetry
{
    public const string Name = "Products";

    public static readonly ActivitySource Source = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> CacheLookups = Meter.CreateCounter<long>(
        "products.cache.lookups", description: "Product reads by cache result (cache.result: hit/miss)");

    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>(
        "products.outbox.published", description: "Outbox messages acknowledged by Kafka");

    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>(
        "products.outbox.failed", description: "Failed outbox deliveries (error.type: transient/permanent)");

    public static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>(
        "products.outbox.dead_lettered", description: "Outbox messages that exceeded the attempt limit");

    public static readonly Histogram<double> OutboxDelay = Meter.CreateHistogram<double>(
        "products.outbox.delay", unit: "s", description: "Time from event occurrence to Kafka acknowledgement");

    public static readonly Counter<long> EventsConsumed = Meter.CreateCounter<long>(
        "products.events.consumed", description: "Consumed events (outcome: handled/duplicate/skipped/failed)");
}
