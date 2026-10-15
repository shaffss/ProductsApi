using System.Diagnostics;
using System.Text.Json;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Core.Events;

namespace Products.UnitTests;

public sealed class ProductPriceChangedTests
{
    private static readonly Product Product = new()
    {
        Id = Guid.Parse("01a13e38-2345-775e-bbe2-8a74877a9eb0"),
        Name = "Czujnik temperatury",
        Price = 129.99m,
        Currency = "EUR",
        CreatedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc),
        Version = 2,
    };

    private static readonly ProductPriceChanged Event =
        ProductPriceChanged.From(new PriceUpdate(Product, OldPrice: 149.99m, OldCurrency: "PLN"));

    [Fact]
    public void Event_contains_previous_and_new_price()
    {
        Assert.Equal(149.99m, Event.OldPrice);
        Assert.Equal(129.99m, Event.NewPrice);
        Assert.Equal("EUR", Event.Currency);
        Assert.Equal("PLN", Event.OldCurrency);
        Assert.Equal(Product.Id, Event.ProductId);
        Assert.Equal(Product.UpdatedAtUtc, Event.ChangedAtUtc);
        Assert.Equal(2, Event.Version);
        Assert.NotEqual(Guid.Empty, Event.EventId);
    }

    [Fact]
    public void Json_matches_event_contract_and_round_trips()
    {
        var json = Event.ToJson();
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(Event.EventId, root.GetProperty("eventId").GetGuid());
        Assert.Equal(Product.Id, root.GetProperty("productId").GetGuid());
        Assert.Equal(149.99m, root.GetProperty("oldPrice").GetDecimal());
        Assert.Equal(129.99m, root.GetProperty("newPrice").GetDecimal());
        Assert.Equal("EUR", root.GetProperty("currency").GetString());
        Assert.Equal("2026-09-17T08:00:00Z", root.GetProperty("changedAtUtc").GetString());
        Assert.Equal(Event, ProductPriceChanged.FromJson(json));
    }

    [Fact]
    public void Outbox_message_is_keyed_by_product()
    {
        var message = Event.ToOutboxMessage();

        Assert.Equal("ProductPriceChanged.v1", message.Type);
        Assert.Equal("product-price-changed", message.Topic);
        Assert.Equal(Product.Id.ToString(), message.MessageKey); // jedna partycja na produkt - zachowana kolejność
        Assert.Equal(Event.ToJson(), message.Payload);
        Assert.Equal(Event.ChangedAtUtc, message.OccurredAtUtc);
        Assert.Null(message.TraceParent); // poza żądaniem (brak bieżącego śladu)
    }

    [Fact]
    public void Outbox_message_carries_current_trace_context()
    {
        using var request = new Activity("PUT /api/products/{id}/price").Start();

        var message = Event.ToOutboxMessage();

        Assert.Equal(request.Id, message.TraceParent); // 00-{trace-id}-{span-id}-{flags}
    }
}
