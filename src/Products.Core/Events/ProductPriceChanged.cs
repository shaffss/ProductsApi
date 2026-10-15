using System.Diagnostics;
using System.Text.Json;
using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Core.Events;

// Zdarzenie publikowane do Kafki. Pola z zadania + OldCurrency (zmiana waluty) i Version (kolejność zdarzeń).
// Currency = waluta nowej ceny.
public sealed record ProductPriceChanged(
    Guid EventId,
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice,
    string Currency,
    string OldCurrency,
    DateTime ChangedAtUtc,
    int Version)
{
    public const string Topic = "product-price-changed";

    // Typ z wersją schematu (nagłówek Kafki event-type): niezgodna zmiana pól = nowa wersja, a konsument
    // rozpoznaje wiadomości, których nie umie obsłużyć, zanim spróbuje je deserializować
    public const string EventType = "ProductPriceChanged.v1";

    // tylko dla faktycznej zmiany (update.Changed)
    public static ProductPriceChanged From(PriceUpdate update) => new(
        Guid.CreateVersion7(),
        update.Product.Id,
        update.OldPrice!.Value,
        update.Product.Price,
        update.Product.Currency,
        update.OldCurrency!,
        update.Product.UpdatedAtUtc,
        update.Product.Version);

    public string ToJson() => JsonSerializer.Serialize(this, EventsJsonContext.Default.ProductPriceChanged);

    public static ProductPriceChanged? FromJson(string json) =>
        JsonSerializer.Deserialize(json, EventsJsonContext.Default.ProductPriceChanged);

    // Klucz wiadomości = ProductId: zdarzenia jednego produktu trafiają do tej samej partycji (zachowana kolejność).
    // TraceParent: bieżący ślad (żądanie HTTP zmiany ceny) - wysyłka i obsługa zdarzenia trafią do tego samego śladu.
    public OutboxMessage ToOutboxMessage() => new()
    {
        Type = EventType,
        Topic = Topic,
        MessageKey = ProductId.ToString(),
        Payload = ToJson(),
        TraceParent = Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.Id : null,
        OccurredAtUtc = ChangedAtUtc,
    };
}
