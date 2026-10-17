using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.UnitTests;

// Proste implementacje w pamięci zamiast mocków - zachowanie jak prawdziwych zależności, liczniki wywołań do asercji

internal sealed class FakeProductRepository : IProductRepository
{
    private static readonly DateTime Now = new(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc);

    public Dictionary<Guid, Product> Products { get; } = new();
    public Dictionary<string, IdempotencyKey> IdempotencyKeys { get; } = new();
    public int GetCalls { get; private set; }
    public int CreateCalls { get; private set; }

    // odczyt z bazy czeka na otwarcie bramki - symulacja wolnego zapytania przy równoczesnych żądaniach
    public TaskCompletionSource? GetGate { get; set; }

    public Product Add(decimal price = 149.99m, string currency = "PLN")
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Czujnik temperatury",
            Price = price,
            Currency = currency,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            Version = 1,
        };
        Products[product.Id] = product;
        return product;
    }

    public async Task<Product?> GetAsync(Guid id, CancellationToken ct)
    {
        GetCalls++;
        if (GetGate is { } gate)
            await gate.Task;
        return Products.GetValueOrDefault(id);
    }

    public Task<Product> CreateAsync(string name, decimal price, string currency, CancellationToken ct)
    {
        CreateCalls++;
        var product = Add(price, currency);
        product.Name = name;
        return Task.FromResult(product);
    }

    public async Task<Product?> TryCreateAsync(
        string name, decimal price, string currency, IdempotencyKey idempotencyKey, CancellationToken ct)
    {
        if (IdempotencyKeys.ContainsKey(idempotencyKey.Key))
            return null;

        idempotencyKey.Product = await CreateAsync(name, price, currency, ct);
        idempotencyKey.ProductId = idempotencyKey.Product.Id;
        IdempotencyKeys[idempotencyKey.Key] = idempotencyKey;
        return idempotencyKey.Product;
    }

    public Task<IdempotencyKey?> GetIdempotencyKeyAsync(string key, CancellationToken ct) =>
        Task.FromResult(IdempotencyKeys.GetValueOrDefault(key));

    public Task<PriceUpdate?> ChangePriceAsync(Guid id, decimal price, string currency, int? expectedVersion, CancellationToken ct)
    {
        if (!Products.TryGetValue(id, out var product))
            return Task.FromResult<PriceUpdate?>(null);
        if (expectedVersion is { } expected && product.Version != expected)
            return Task.FromResult<PriceUpdate?>(new PriceUpdate(product, null, null, VersionConflict: true));
        if (product.Price == price && product.Currency == currency)
            return Task.FromResult<PriceUpdate?>(new PriceUpdate(product, null, null));

        var (oldPrice, oldCurrency) = (product.Price, product.Currency);
        product.Price = price;
        product.Currency = currency;
        product.UpdatedAtUtc = product.UpdatedAtUtc.AddMinutes(1);
        product.Version++;
        return Task.FromResult<PriceUpdate?>(new PriceUpdate(product, oldPrice, oldCurrency));
    }

    public Task<ProductPage> ListAsync(int skip, int take, CancellationToken ct)
    {
        LastListRequest = (skip, take);
        var items = Products.Values.OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id).Skip(skip).Take(take).ToList();
        return Task.FromResult(new ProductPage(items, Products.Count, skip, take));
    }

    public (int Skip, int Take)? LastListRequest { get; private set; }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(Products.Remove(id));
}

internal sealed class FakeProductCache : IProductCache
{
    public bool IsEnabled { get; init; } = true;
    public Dictionary<Guid, Product> Entries { get; } = new();
    public List<Guid> Removed { get; } = [];

    public ValueTask<Product?> GetAsync(Guid id) => ValueTask.FromResult(Entries.GetValueOrDefault(id));

    public ValueTask SetAsync(Product product)
    {
        Entries[product.Id] = product;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryRemoveAsync(Guid id)
    {
        Removed.Add(id);
        Entries.Remove(id);
        return ValueTask.FromResult(true);
    }
}

internal sealed class FakeCurrencyRepository : ICurrencyRepository
{
    public List<Currency> Currencies { get; } =
    [
        new() { Id = 1, Code = "PLN", Name = "Polski złoty" },
        new() { Id = 2, Code = "EUR", Name = "Euro" },
        new() { Id = 3, Code = "USD", Name = "Dolar amerykański" },
    ];

    public int Calls { get; private set; }

    public Task<IReadOnlyList<Currency>> GetActiveAsync(CancellationToken ct)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<Currency>>(Currencies.Where(c => c.IsActive).ToList());
    }
}

// Zegar przesuwany ręcznie - test TTL bez czekania
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}
