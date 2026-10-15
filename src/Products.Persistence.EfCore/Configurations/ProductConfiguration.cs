using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Core.Entities;

namespace Products.Persistence.EfCore.Configurations;

internal sealed class ProductConfiguration(SqlDialect sql) : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Id).HasDefaultValueSql(sql.NewGuidSql);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasColumnType(sql.CurrencyCodeType);
        builder.Property(p => p.CreatedAtUtc).HasDefaultValueSql(sql.UtcNowSql);
        builder.Property(p => p.UpdatedAtUtc).HasDefaultValueSql(sql.UtcNowSql);
        builder.Property(p => p.Version).HasDefaultValue(1);

        builder.HasOne<Currency>().WithMany()
            .HasForeignKey(p => p.Currency).HasPrincipalKey(c => c.Code)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_Name_NotEmpty", $"TRIM({sql.Quote("Name")}) <> ''");
            t.HasCheckConstraint("CK_Products_Price_Positive", $"{sql.Quote("Price")} > 0");
        });
    }
}
