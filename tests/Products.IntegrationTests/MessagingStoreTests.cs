using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Core.Events;
using Products.IntegrationTests.Fixtures;
using Products.Persistence;
using Products.Persistence.Dapper;

namespace Products.IntegrationTests;

// Outbox, idempotentny konsument i retencja (ADO.NET, wspólne dla obu ORM) - dla każdej bazy.
// Własna baza klasy (testy klasy idą po kolei); wysyłka obejmuje wszystkie oczekujące wiadomości,
// więc każdy test zaczyna od opróżnienia outboxa.
public abstract class MessagingStoreTests(DatabaseFixture database) : IAsyncLifetime
{
    private const int MaxAttempts = 10;

    private ServiceProvider _services = null!;
    private IProductRepository _products = null!;
    private IOutboxStore _outbox = null!;
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _services = new ServiceCollection()
            .AddDapperPersistence(database.Engine, await database.GetSharedDatabaseAsync("messaging"))
            .BuildServiceProvider();
        _products = _services.GetRequiredService<IProductRepository>();
        _outbox = _services.GetRequiredService<IOutboxStore>();
        _db = new(_services.GetRequiredService<DbDataSource>(), _services.GetRequiredService<SqlDialect>());

        while (await _outbox.ProcessBatchAsync(1000, MaxAttempts, PublishAll, Ct) > 0)
        {
        }
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task Outbox_publishes_pending_messages_in_order_and_marks_them_processed()
    {
        var productId = await ProductWithPriceChangesAsync(10m, 20m, 30m);
        var published = new List<OutboxMessage>();

        var count = await _outbox.ProcessBatchAsync(100, MaxAttempts, (batch, _) =>
        {
            published.AddRange(batch);
            return Task.FromResult(new OutboxPublishResult(batch.Count, null));
        }, Ct);

        Assert.Equal(3, count);
        Assert.Equal([10m, 20m, 30m], published.Select(m => ProductPriceChanged.FromJson(m.Payload)!.NewPrice));
        Assert.All(published, m => Assert.Equal(ProductPriceChanged.EventType, m.Type));
        Assert.All(await _db.GetOutboxAsync(productId), row => Assert.NotNull(row.ProcessedAtUtc));
        Assert.Equal(0, await _outbox.ProcessBatchAsync(100, MaxAttempts, NotExpected, Ct));
    }

    [Fact]
    public async Task Outbox_message_carries_trace_context_of_the_request()
    {
        using var request = new Activity("PUT /api/products/{id}/price").Start();
        var productId = await ProductWithPriceChangesAsync(10m);
        request.Stop();

        IReadOnlyList<OutboxMessage>? published = null;
        await _outbox.ProcessBatchAsync(100, MaxAttempts, (batch, _) =>
        {
            published = batch;
            return Task.FromResult(new OutboxPublishResult(batch.Count, null));
        }, Ct);

        Assert.Equal(request.Id, Assert.Single(published!).TraceParent);
        Assert.Equal(request.Id, Assert.Single(await _db.GetOutboxAsync(productId)).TraceParent);
    }

    [Fact]
    public async Task Outbox_keeps_undelivered_message_pending_with_error()
    {
        var productId = await ProductWithPriceChangesAsync(10m, 20m);

        var count = await _outbox.ProcessBatchAsync(
            100, MaxAttempts, (_, _) => Task.FromResult(new OutboxPublishResult(1, "Broker unavailable")), Ct);

        Assert.Equal(1, count);
        var rows = await _db.GetOutboxAsync(productId);
        Assert.NotNull(rows[0].ProcessedAtUtc);
        // błąd przejściowy (brak brokera) nie zużywa prób - inaczej awaria Kafki przeniosłaby wszystko do dead-letter
        Assert.Equal((null, 0, "Broker unavailable"), (rows[1].ProcessedAtUtc, rows[1].Attempts, rows[1].LastError));

        // kolejna paczka zaczyna od niedostarczonej wiadomości
        IReadOnlyList<OutboxMessage>? retried = null;
        await _outbox.ProcessBatchAsync(100, MaxAttempts, (batch, _) =>
        {
            retried = batch;
            return Task.FromResult(new OutboxPublishResult(batch.Count, null));
        }, Ct);
        Assert.Equal(20m, ProductPriceChanged.FromJson(Assert.Single(retried!).Payload)!.NewPrice);
    }

    [Fact]
    public async Task Outbox_moves_message_to_dead_letter_after_max_permanent_failures()
    {
        var productId = await ProductWithPriceChangesAsync(10m, 20m);

        Assert.Equal(0, await _outbox.ProcessBatchAsync(100, maxAttempts: 2, FailPermanently, Ct));
        Assert.Equal(0, await _outbox.ProcessBatchAsync(100, maxAttempts: 2, FailPermanently, Ct));

        // limit prób osiągnięty - wiadomość pomijana, kolejne nie czekają na nią w nieskończoność
        IReadOnlyList<OutboxMessage>? next = null;
        await _outbox.ProcessBatchAsync(100, maxAttempts: 2, (batch, _) =>
        {
            next = batch;
            return Task.FromResult(new OutboxPublishResult(batch.Count, null));
        }, Ct);
        Assert.Equal(20m, ProductPriceChanged.FromJson(Assert.Single(next!).Payload)!.NewPrice);

        var rows = await _db.GetOutboxAsync(productId);
        Assert.Equal((null, 2, "Message too large"), (rows[0].ProcessedAtUtc, rows[0].Attempts, rows[0].LastError));
        Assert.NotNull(rows[1].ProcessedAtUtc);
    }

    [Fact]
    public async Task Outbox_is_processed_by_one_instance_at_a_time()
    {
        await ProductWithPriceChangesAsync(10m);
        var batchTaken = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        // pierwsza "instancja" trzyma blokadę, dopóki publikacja trwa
        var first = _outbox.ProcessBatchAsync(100, MaxAttempts, async (batch, _) =>
        {
            batchTaken.SetResult();
            await release.Task;
            return new OutboxPublishResult(batch.Count, null);
        }, Ct);
        await batchTaken.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        var second = await _outbox.ProcessBatchAsync(100, MaxAttempts, NotExpected, Ct);
        release.SetResult();

        Assert.Null(second); // blokada zajęta - druga instancja nic nie wysyła
        Assert.Equal(1, await first);
    }

    [Fact]
    public async Task Event_is_handled_only_once_per_consumer()
    {
        var store = _services.GetRequiredService<IProcessedEventStore>();
        var eventId = Guid.CreateVersion7();
        var handled = new List<string>();
        Func<CancellationToken, Task> Handle(string consumer) => _ => { handled.Add(consumer); return Task.CompletedTask; };

        Assert.True(await store.TryProcessAsync("api-efcore", eventId, Handle("api-efcore"), Ct));
        Assert.False(await store.TryProcessAsync("api-efcore", eventId, Handle("api-efcore"), Ct)); // duplikat
        // inny odbiorca (grupa konsumentów) na tej samej bazie obsługuje zdarzenie niezależnie
        Assert.True(await store.TryProcessAsync("api-dapper", eventId, Handle("api-dapper"), Ct));

        Assert.Equal(["api-efcore", "api-dapper"], handled);
        Assert.True(await _db.IsEventProcessedAsync(eventId));
    }

    [Fact]
    public async Task Failed_event_handling_is_rolled_back_and_can_be_retried()
    {
        var store = _services.GetRequiredService<IProcessedEventStore>();
        var eventId = Guid.CreateVersion7();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.TryProcessAsync("api", eventId, _ => throw new InvalidOperationException("Redis unavailable"), Ct));
        Assert.False(await _db.IsEventProcessedAsync(eventId));

        Assert.True(await store.TryProcessAsync("api", eventId, _ => Task.CompletedTask, Ct));
    }

    [Fact]
    public async Task Retention_deletes_only_expired_technical_data()
    {
        var options = new RetentionOptions();
        var expired = DateTime.UtcNow - options.OutboxMessages - TimeSpan.FromDays(1);

        // outbox: stara wysłana (do usunięcia), stara w dead-letter i świeża wysłana (zostają)
        var sent = await ProductWithPriceChangesAsync(10m);
        var deadLetter = await ProductWithPriceChangesAsync(20m);
        await _outbox.ProcessBatchAsync(100, MaxAttempts, (batch, _) =>
            Task.FromResult(new OutboxPublishResult(1, "Message too large", Permanent: true)), Ct);
        var recent = await ProductWithPriceChangesAsync(30m);
        await _outbox.ProcessBatchAsync(100, maxAttempts: 1, PublishAll, Ct); // limit 1: wiadomość deadLetter pominięta
        await _db.SetDateAsync("OutboxMessages", "ProcessedAtUtc", "MessageKey", sent.ToString(), expired);
        await _db.SetDateAsync("OutboxMessages", "OccurredAtUtc", "MessageKey", deadLetter.ToString(), expired);

        // obsłużone zdarzenia i klucze idempotencji: stare i świeże
        var store = _services.GetRequiredService<IProcessedEventStore>();
        var (oldEvent, newEvent) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        foreach (var eventId in new[] { oldEvent, newEvent })
            await store.TryProcessAsync("api", eventId, _ => Task.CompletedTask, Ct);
        await _db.SetDateAsync("ProcessedEvents", "ProcessedAtUtc", "EventId", oldEvent, expired);
        var (oldKey, newKey) = ($"old-{Guid.NewGuid():N}", $"new-{Guid.NewGuid():N}");
        foreach (var key in new[] { oldKey, newKey })
            await _products.TryCreateAsync("Czujnik ruchu PIR", 1m, "PLN", new IdempotencyKey { Key = key, RequestHash = new byte[32] }, Ct);
        await _db.SetDateAsync("IdempotencyKeys", "CreatedAtUtc", "Key", oldKey, expired);

        await new DatabaseRetention(_services.GetRequiredService<DbDataSource>(), _services.GetRequiredService<SqlDialect>(),
            options, TimeProvider.System, NullLogger<DatabaseRetention>.Instance).RunOnceAsync(Ct);

        Assert.Empty(await _db.GetOutboxAsync(sent));
        Assert.Equal(1, Assert.Single(await _db.GetOutboxAsync(deadLetter)).Attempts);
        Assert.NotNull(Assert.Single(await _db.GetOutboxAsync(recent)).ProcessedAtUtc);
        Assert.Equal((false, true), (await _db.IsEventProcessedAsync(oldEvent), await _db.IsEventProcessedAsync(newEvent)));
        Assert.Equal((false, true), (await _db.IdempotencyKeyExistsAsync(oldKey), await _db.IdempotencyKeyExistsAsync(newKey)));
    }

    private async Task<Guid> ProductWithPriceChangesAsync(params decimal[] prices)
    {
        var product = await _products.CreateAsync("Czujnik ruchu PIR", 1m, "PLN", Ct);
        foreach (var price in prices)
            await _products.ChangePriceAsync(product.Id, price, "PLN", expectedVersion: null, Ct);
        return product.Id;
    }

    private static Task<OutboxPublishResult> PublishAll(IReadOnlyList<OutboxMessage> batch, CancellationToken ct) =>
        Task.FromResult(new OutboxPublishResult(batch.Count, null));

    private static Task<OutboxPublishResult> FailPermanently(IReadOnlyList<OutboxMessage> batch, CancellationToken ct) =>
        Task.FromResult(new OutboxPublishResult(0, "Message too large", Permanent: true));

    private static Task<OutboxPublishResult> NotExpected(IReadOnlyList<OutboxMessage> batch, CancellationToken ct) =>
        throw new InvalidOperationException("No batch expected");
}

public sealed class SqlServerMessagingStoreTests(SqlServerFixture database) : MessagingStoreTests(database);

public sealed class PostgresMessagingStoreTests(PostgresFixture database) : MessagingStoreTests(database);
