using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Products.Core.Entities;
using Products.Persistence.EfCore.Configurations;

namespace Products.Persistence.EfCore;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options) : DbContext(options)
{
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPriceHistory> ProductPriceHistory => Set<ProductPriceHistory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Indeksy definiujemy jawnie. Automatyczny indeks na każdym kluczu obcym (np. Products.Currency - 3 wartości)
        // nie przyspiesza odczytów, a spowalnia każdy zapis.
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();

        // datetime2 nie przechowuje strefy - bez tego daty z SQL Server miałyby Kind=Unspecified
        // (w JSON bez "Z"), a z Postgresa (timestamptz) Kind=Utc
        if (Database.IsSqlServer())
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // model budowany i cache'owany osobno dla każdego providera, więc rozróżnienie jest bezpieczne
        var sql = Database.IsNpgsql() ? SqlDialect.Postgres : SqlDialect.SqlServer;

        modelBuilder
            .ApplyConfiguration(new CurrencyConfiguration(sql))
            .ApplyConfiguration(new ProductConfiguration(sql))
            .ApplyConfiguration(new ProductPriceHistoryConfiguration(sql))
            .ApplyConfiguration(new OutboxMessageConfiguration(sql))
            .ApplyConfiguration(new ProcessedEventConfiguration())
            .ApplyConfiguration(new IdempotencyKeyConfiguration(sql));
    }
}
