namespace Products.Infrastructure.Messaging;

// Nagłówki wiadomości Kafki
internal static class KafkaHeaders
{
    public const string EventType = "event-type"; // typ zdarzenia z wersją schematu, np. ProductPriceChanged.v1
    public const string TraceParent = "traceparent"; // W3C trace context - jeden ślad od żądania HTTP do konsumenta
}
