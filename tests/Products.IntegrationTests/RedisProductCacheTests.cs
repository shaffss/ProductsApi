using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Infrastructure;
using Products.Infrastructure.Caching;
using Products.IntegrationTests.Fixtures;
using StackExchange.Redis;

namespace Products.IntegrationTests;

public sealed class RedisProductCacheTests(RedisFixture redis)
{
    private static readonly Product Product = new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Termostat programowalny",
        Price = 349m,
        Currency = "PLN",
        CreatedAtUtc = new DateTime(2026, 9, 4, 13, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 9, 4, 13, 0, 0, DateTimeKind.Utc),
        Version = 1,
    };

    [Fact]
    public async Task Product_is_cached_under_product_key_with_ttl_and_can_be_removed()
    {
        await using var services = CreateServices(redis.ConnectionString);
        var cache = services.GetRequiredService<IProductCache>();

        await cache.SetAsync(Product); // fire-and-forget - zapis kończy się asynchronicznie
        var cached = await WaitForAsync(() => cache.GetAsync(Product.Id));

        Assert.Equal(Product.Name, cached?.Name);
        Assert.Equal(Product.UpdatedAtUtc, cached?.UpdatedAtUtc);
        var ttl = await services.GetRequiredService<IConnectionMultiplexer>().GetDatabase().KeyTimeToLiveAsync($"product:{Product.Id}");
        Assert.InRange(ttl!.Value, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));

        Assert.True(await cache.TryRemoveAsync(Product.Id));
        Assert.Null(await cache.GetAsync(Product.Id));
    }

    [Fact]
    public async Task Invalid_cache_entry_is_treated_as_cache_miss()
    {
        await using var services = CreateServices(redis.ConnectionString);
        var cache = services.GetRequiredService<IProductCache>();
        var redisDb = services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var id = Guid.CreateVersion7();

        // np. wpis z poprzedniej wersji aplikacji o innym kształcie Product
        await redisDb.StringSetAsync($"product:{id}", """{"stale":true}""");

        Assert.Null(await cache.GetAsync(id)); // odczyt pójdzie do bazy zamiast błędu 500
    }

    [Fact]
    public async Task Unavailable_redis_does_not_break_operations()
    {
        await using var services = CreateServices("localhost:1,connectTimeout=500"); // nic tam nie nasłuchuje
        var cache = services.GetRequiredService<IProductCache>();

        var stopwatch = Stopwatch.StartNew();
        Assert.Null(await cache.GetAsync(Product.Id)); // brak cache = odczyt z bazy
        await cache.SetAsync(Product);
        Assert.False(await cache.TryRemoveAsync(Product.Id)); // konsument zdarzeń ponowi unieważnienie

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"took {stopwatch.Elapsed}"); // bez czekania na timeouty
    }

    private static ServiceProvider CreateServices(string connectionString) => new ServiceCollection()
        .AddLogging()
        .AddProductCache(new CacheOptions { ProductTtl = TimeSpan.FromMinutes(5) }, connectionString)
        .BuildServiceProvider();

    private static async Task<Product?> WaitForAsync(Func<ValueTask<Product?>> read)
    {
        for (var i = 0; i < 50; i++)
        {
            if (await read() is { } product)
                return product;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        return null;
    }
}
