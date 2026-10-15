using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Core.Entities;

namespace Products.Persistence.EfCore.Configurations;

internal sealed class IdempotencyKeyConfiguration(SqlDialect sql) : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.HasKey(k => k.Key);
        builder.Property(k => k.Key).HasMaxLength(100).IsUnicode(false);
        builder.Property(k => k.RequestHash).HasMaxLength(32).IsFixedLength(); // SHA-256
        builder.Property(k => k.CreatedAtUtc).HasDefaultValueSql(sql.UtcNowSql);

        builder.HasOne(k => k.Product).WithMany()
            .HasForeignKey(k => k.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(k => k.CreatedAtUtc); // retencja: klucze ważne ograniczony czas
    }
}
