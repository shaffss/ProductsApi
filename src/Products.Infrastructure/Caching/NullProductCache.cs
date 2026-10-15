using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Infrastructure.Caching;

// Cache wyłączony (Cache:Enabled = false) - każdy odczyt idzie do bazy
internal sealed class NullProductCache : IProductCache
{
    public bool IsEnabled => false;

    public ValueTask<Product?> GetAsync(Guid id) => ValueTask.FromResult<Product?>(null);

    public ValueTask SetAsync(Product product) => ValueTask.CompletedTask;

    public ValueTask<bool> TryRemoveAsync(Guid id) => ValueTask.FromResult(true);
}
