using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Core.Entities;

namespace Products.Persistence.EfCore.Configurations;

internal sealed class CurrencyConfiguration(SqlDialect sql) : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.HasAlternateKey(c => c.Code);
        builder.Property(c => c.Code).HasColumnType(sql.CurrencyCodeType);
        builder.Property(c => c.Name).HasMaxLength(100);

        // sentinel true: EF pomija kolumnę przy INSERT tylko dla true (wtedy działa DEFAULT),
        // jawne false trafia do bazy - przy domyślnym sentinelu false zostałoby zamienione na true
        builder.Property(c => c.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.HasData(
            new Currency { Id = 1, Code = "PLN", Name = "Polski złoty" },
            new Currency { Id = 2, Code = "EUR", Name = "Euro" },
            new Currency { Id = 3, Code = "USD", Name = "Dolar amerykański" });
    }
}
