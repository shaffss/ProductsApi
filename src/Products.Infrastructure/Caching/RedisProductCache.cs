using System.Text.Json;
using Microsoft.Extensions.Logging;
using Products.Core.Abstractions;
using Products.Core.Entities;
using StackExchange.Redis;

namespace Products.Infrastructure.Caching;

// Klucz product:{id} -> JSON produktu (string z TTL). Niedostępny Redis = brak cache, nie błąd żądania.
internal sealed partial class RedisProductCache(IConnectionMultiplexer redis, CacheOptions options, ILogger<RedisProductCache> logger)
    : IProductCache
{
    private readonly IDatabase _db = redis.GetDatabase();

    public bool IsEnabled => true;

    public async ValueTask<Product?> GetAsync(Guid id)
    {
        try
        {
            ReadOnlyMemory<byte> json = await _db.StringGetAsync(Key(id));
            return json.IsEmpty ? null : JsonSerializer.Deserialize(json.Span, CacheJsonContext.Default.Product);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            LogReadFailed(logger, e, id);
            return null;
        }
        catch (JsonException e)
        {
            // wpis w innym formacie (np. po wdrożeniu zmienionego Product) - jak brak w cache, nadpisze go odczyt z bazy
            LogInvalidEntry(logger, e, id);
            return null;
        }
    }

    public ValueTask SetAsync(Product product)
    {
        try
        {
            // FireAndForget: odpowiedź nie czeka na potwierdzenie zapisu do cache
            _db.StringSet(Key(product.Id), JsonSerializer.SerializeToUtf8Bytes(product, CacheJsonContext.Default.Product),
                options.ProductTtl, flags: CommandFlags.FireAndForget);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            LogWriteFailed(logger, e, product.Id);
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask<bool> TryRemoveAsync(Guid id)
    {
        try
        {
            await _db.KeyDeleteAsync(Key(id));
            return true;
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            // baza ma już nowe dane; wpis usunie ponownie konsument zdarzenia, najpóźniej zniknie po TTL
            LogRemoveFailed(logger, e, id);
            return false;
        }
    }

    private static RedisKey Key(Guid id) => $"product:{id}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis unavailable - product {ProductId} read from database")]
    private static partial void LogReadFailed(ILogger logger, Exception exception, Guid productId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid cache entry for product {ProductId} - read from database")]
    private static partial void LogInvalidEntry(ILogger logger, Exception exception, Guid productId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis unavailable - product {ProductId} not cached")]
    private static partial void LogWriteFailed(ILogger logger, Exception exception, Guid productId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache invalidation failed for product {ProductId}")]
    private static partial void LogRemoveFailed(ILogger logger, Exception exception, Guid productId);
}
