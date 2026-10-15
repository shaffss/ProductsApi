namespace Products.Infrastructure.Caching;

// Sekcja "Cache" konfiguracji; Enabled = false np. w benchmarkach dostępu do bazy
public sealed class CacheOptions
{
    public const string Section = "Cache";

    public bool Enabled { get; set; } = true;
    public TimeSpan ProductTtl { get; set; } = TimeSpan.FromMinutes(5);
}
