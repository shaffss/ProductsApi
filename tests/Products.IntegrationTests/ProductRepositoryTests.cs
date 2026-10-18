using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Core.Events;
using Products.IntegrationTests.Fixtures;
using Products.Persistence;
using Products.Persistence.Dapper;
using Products.Persistence.EfCore;

namespace Products.IntegrationTests;

// Te same testy dla każdego z 4 wariantów (ORM x baza) - na prawdziwej bazie ze schematem z migracji
public abstract class ProductRepositoryTests(DatabaseFixture database, OrmKind orm) : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private IProductRepository _repository = null!;
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // jedna baza na silnik dla wszystkich wariantów ORM - każdy test tworzy własne produkty
        var connectionString = await database.GetSharedDatabaseAsync("repositories");
        var services = new ServiceCollection();
        if (orm == OrmKind.Dapper)
            services.AddDapperPersistence(database.Engine, connectionString);
        else
            services.AddEfCorePersistence(database.Engine, connectionString);
        _services = services.BuildServiceProvider();

        _repository = _services.GetRequiredService<IProductRepository>();
        _db = new(_services.GetRequiredService<DbDataSource>(), _services.GetRequiredService<SqlDialect>());
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task Create_returns_values_generated_by_database()
    {
        var product = await _repository.CreateAsync("Czujnik temperatury", 149.99m, "PLN", Ct);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(1, product.Version);
        Assert.Equal(DateTimeKind.Utc, product.CreatedAtUtc.Kind);
        Assert.InRange(product.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(product.CreatedAtUtc, product.UpdatedAtUtc);
    }

    [Fact]
    public async Task Get_returns_saved_product_or_null()
    {
        var created = await _repository.CreateAsync("Czujnik wilgotności", 189m, "EUR", Ct);

        var read = await _repository.GetAsync(created.Id, Ct);

        Assert.NotNull(read);
        Assert.Equal((created.Name, 189m, "EUR", created.UpdatedAtUtc), (read.Name, read.Price, read.Currency, read.UpdatedAtUtc));
        Assert.Equal(DateTimeKind.Utc, read.UpdatedAtUtc.Kind);
        Assert.Null(await _repository.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ChangePrice_returns_previous_price_and_records_history_and_event()
    {
        var created = await _repository.CreateAsync("Sterownik PLC", 100m, "PLN", Ct);
        // data zmiany minutę wstecz: zmiana ceny tuż po utworzeniu (ten sam takt zegara bazy, np. SQL Server
        // na runnerze CI) dałaby ten sam znacznik czasu, a test ma sprawdzić, że zmiana odświeża UpdatedAtUtc
        var before = created.UpdatedAtUtc.AddMinutes(-1);
        await _db.SetDateAsync("Products", "UpdatedAtUtc", "Id", created.Id, before);

        var update = await _repository.ChangePriceAsync(created.Id, 90.5m, "EUR", expectedVersion: null, Ct);

        Assert.NotNull(update);
        Assert.True(update.Changed);
        Assert.Equal((100m, "PLN"), (update.OldPrice, update.OldCurrency));
        Assert.Equal((90.5m, "EUR", 2), (update.Product.Price, update.Product.Currency, update.Product.Version));
        Assert.Equal(created.Name, update.Product.Name);
        Assert.True(update.Product.UpdatedAtUtc > before);

        Assert.Equal([(100m, 90.5m)], await _db.GetHistoryAsync(created.Id));
        var outbox = Assert.Single(await _db.GetOutboxAsync(created.Id));
        var priceChanged = ProductPriceChanged.FromJson(outbox.Payload);
        Assert.Equal((100m, 90.5m, "EUR", "PLN", 2),
            (priceChanged?.OldPrice, priceChanged?.NewPrice, priceChanged?.Currency, priceChanged?.OldCurrency, priceChanged?.Version));
    }

    [Fact]
    public async Task ChangePrice_with_same_price_and_currency_writes_nothing()
    {
        var created = await _repository.CreateAsync("Przekaźnik czasowy", 89.9m, "PLN", Ct);

        var update = await _repository.ChangePriceAsync(created.Id, 89.9m, "PLN", expectedVersion: null, Ct);

        Assert.NotNull(update);
        Assert.False(update.Changed);
        Assert.Equal((1, created.UpdatedAtUtc), (update.Product.Version, update.Product.UpdatedAtUtc));
        Assert.Empty(await _db.GetHistoryAsync(created.Id));
        Assert.Empty(await _db.GetOutboxAsync(created.Id));
    }

    [Fact]
    public async Task ChangePrice_of_missing_product_returns_null()
    {
        Assert.Null(await _repository.ChangePriceAsync(Guid.NewGuid(), 10m, "PLN", expectedVersion: null, Ct));
    }

    [Fact]
    public async Task Concurrent_price_changes_lose_no_update()
    {
        var created = await _repository.CreateAsync("Zasilacz 24 V DC", 59m, "PLN", Ct);
        var prices = Enumerable.Range(1, 20).Select(i => 100m + i).ToArray();

        var updates = await Task.WhenAll(prices.Select(p => _repository.ChangePriceAsync(created.Id, p, "PLN", expectedVersion: null, Ct)));

        Assert.All(updates, u => Assert.True(u?.Changed));
        var history = await _db.GetHistoryAsync(created.Id);
        Assert.Equal(20, history.Count);
        Assert.Equal(59m, history[0].OldPrice);
        // każda zmiana widzi poprzednią: stara cena = nowa cena poprzedniego wpisu (atomowy UPDATE, brak utraconych zmian)
        Assert.All(history.Zip(history.Skip(1)), pair => Assert.Equal(pair.First.NewPrice, pair.Second.OldPrice));

        var final = await _repository.GetAsync(created.Id, Ct);
        Assert.Equal((21, history[^1].NewPrice), (final?.Version, final?.Price));
        Assert.Equal(20, (await _db.GetOutboxAsync(created.Id)).Count);
    }

    [Fact]
    public async Task ChangePrice_with_stale_expected_version_changes_nothing()
    {
        var created = await _repository.CreateAsync("Sterownik oświetlenia", 100m, "PLN", Ct);
        var first = await _repository.ChangePriceAsync(created.Id, 110m, "PLN", expectedVersion: 1, Ct);

        // druga zmiana z tą samą (już nieaktualną) wersją - jak drugi klient z ETag "1"
        var stale = await _repository.ChangePriceAsync(created.Id, 120m, "PLN", expectedVersion: 1, Ct);

        Assert.True(first?.Changed);
        Assert.Equal((false, true), (stale?.Changed, stale?.VersionConflict));
        Assert.Equal((110m, 2), (stale?.Product.Price, stale?.Product.Version));
        Assert.Equal([(100m, 110m)], await _db.GetHistoryAsync(created.Id));
        Assert.Single(await _db.GetOutboxAsync(created.Id));
    }

    [Fact]
    public async Task TryCreate_with_used_idempotency_key_saves_nothing()
    {
        var key = new IdempotencyKey { Key = $"key-{Guid.NewGuid():N}", RequestHash = [.. Enumerable.Range(0, 32).Select(i => (byte)i)] };
        var name = $"Czujnik zalania {key.Key}";
        var created = await _repository.TryCreateAsync(name, 59m, "PLN", key, Ct);

        var retried = await _repository.TryCreateAsync(name, 59m, "PLN", new IdempotencyKey { Key = key.Key, RequestHash = key.RequestHash }, Ct);

        Assert.NotNull(created);
        Assert.Null(retried); // klucz już użyty - produkt ponowionego żądania wycofany
        Assert.Equal(1, await _db.CountProductsAsync(name));
        var stored = await _repository.GetIdempotencyKeyAsync(key.Key, Ct);
        Assert.Equal((created.Id, name), (stored?.Product?.Id, stored?.Product?.Name));
        Assert.Equal(key.RequestHash, stored?.RequestHash);
        Assert.Equal(DateTimeKind.Utc, stored?.Product?.UpdatedAtUtc.Kind);
        Assert.Null(await _repository.GetIdempotencyKeyAsync($"missing-{key.Key}", Ct));
    }

    [Fact]
    public async Task Concurrent_creates_with_same_idempotency_key_create_one_product()
    {
        var key = $"key-{Guid.NewGuid():N}";
        var name = $"Czujnik dymu {key}";

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            _repository.TryCreateAsync(name, 79m, "PLN", new IdempotencyKey { Key = key, RequestHash = new byte[32] }, Ct)));

        Assert.Single(results, product => product is not null);
        Assert.Equal(1, await _db.CountProductsAsync(name));
    }

    [Fact]
    public async Task List_returns_newest_first_with_total()
    {
        var before = (await _repository.ListAsync(0, 1, Ct)).Total;
        List<Product> created = [];
        for (var i = 0; i < 3; i++)
            created.Add(await _repository.CreateAsync($"Czujnik listy {i}", 10m + i, "PLN", Ct));

        var page = await _repository.ListAsync(0, 3, Ct);
        var second = await _repository.ListAsync(1, 1, Ct);

        Assert.Equal(before + 3, page.Total);
        Assert.Equal(created.Select(p => p.Id).Reverse(), page.Items.Select(p => p.Id));
        Assert.Equal(created[1].Id, second.Items.Single().Id);
        Assert.Equal((1, 1), (second.Skip, second.Take));
        Assert.All(page.Items, p => Assert.Equal(DateTimeKind.Utc, p.UpdatedAtUtc.Kind));
    }

    // Usuwanie produktów wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty
    // [Fact]
    // public async Task Delete_removes_product_with_price_history_and_idempotency_key()
    // {
    //     var key = new IdempotencyKey { Key = $"key-{Guid.NewGuid():N}", RequestHash = new byte[32] };
    //     var product = (await _repository.TryCreateAsync("Czujnik do usunięcia", 10m, "PLN", key, Ct))!;
    //     await _repository.ChangePriceAsync(product.Id, 12m, "PLN", null, Ct);
    //
    //     Assert.True(await _repository.DeleteAsync(product.Id, Ct));
    //
    //     Assert.Null(await _repository.GetAsync(product.Id, Ct));
    //     Assert.Empty(await _db.GetHistoryAsync(product.Id));
    //     Assert.False(await _db.IdempotencyKeyExistsAsync(key.Key));
    //     Assert.False(await _repository.DeleteAsync(product.Id, Ct)); // już nie istnieje
    // }

    [Fact]
    public async Task Currencies_are_seeded_by_migration()
    {
        var currencies = await _services.GetRequiredService<ICurrencyRepository>().GetActiveAsync(Ct);

        Assert.Equal(["EUR", "PLN", "USD"], currencies.Select(c => c.Code).Order());
    }
}

public sealed class EfCoreSqlServerRepositoryTests(SqlServerFixture database) : ProductRepositoryTests(database, OrmKind.EfCore);

public sealed class EfCorePostgresRepositoryTests(PostgresFixture database) : ProductRepositoryTests(database, OrmKind.EfCore);

public sealed class DapperSqlServerRepositoryTests(SqlServerFixture database) : ProductRepositoryTests(database, OrmKind.Dapper);

public sealed class DapperPostgresRepositoryTests(PostgresFixture database) : ProductRepositoryTests(database, OrmKind.Dapper);
