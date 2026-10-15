using Products.Core.Entities;

namespace Products.Core.Abstractions;

// Cache odczytu produktu (cache-aside). Błędy cache nie przerywają operacji - baza jest źródłem prawdy.
public interface IProductCache
{
    // false = cache wyłączony w konfiguracji (np. benchmarki bazy) - każdy odczyt idzie do bazy
    bool IsEnabled { get; }

    ValueTask<Product?> GetAsync(Guid id);

    ValueTask SetAsync(Product product);

    // false = cache niedostępny, wpis mógł zostać (unieważnienie ponowi konsument zdarzeń)
    ValueTask<bool> TryRemoveAsync(Guid id);
}
