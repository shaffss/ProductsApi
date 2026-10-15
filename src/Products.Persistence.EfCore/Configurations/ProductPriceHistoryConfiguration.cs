using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Core.Entities;

namespace Products.Persistence.EfCore.Configurations;

internal sealed class ProductPriceHistoryConfiguration(SqlDialect sql) : IEntityTypeConfiguration<ProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistory> builder)
    {
        builder.Property(h => h.OldPrice).HasPrecision(18, 2);
        builder.Property(h => h.NewPrice).HasPrecision(18, 2);
        builder.Property(h => h.OldCurrency).HasColumnType(sql.CurrencyCodeType);
        builder.Property(h => h.NewCurrency).HasColumnType(sql.CurrencyCodeType);

        builder.HasOne<Product>().WithMany()
            .HasForeignKey(h => h.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // historia produktu chronologicznie; obsługuje też sprawdzanie klucza obcego
        builder.HasIndex(h => new { h.ProductId, h.ChangedAtUtc });
    }
}
