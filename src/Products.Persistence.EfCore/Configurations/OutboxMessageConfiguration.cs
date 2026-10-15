using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Core.Entities;

namespace Products.Persistence.EfCore.Configurations;

internal sealed class OutboxMessageConfiguration(SqlDialect sql) : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(o => o.Type).HasMaxLength(200).IsUnicode(false);
        builder.Property(o => o.Topic).HasMaxLength(200).IsUnicode(false);
        builder.Property(o => o.MessageKey).HasMaxLength(100).IsUnicode(false);
        builder.Property(o => o.TraceParent).HasMaxLength(55).IsUnicode(false); // 00-{32 hex}-{16 hex}-{2 hex}
        builder.Property(o => o.LastError).HasMaxLength(2000);
        builder.Property(o => o.Attempts).HasDefaultValue(0);

        // mały indeks tylko z nieprzetworzonymi wiadomościami
        builder.HasIndex(o => o.Id, "IX_OutboxMessages_Pending")
            .HasFilter($"{sql.Quote("ProcessedAtUtc")} IS NULL");

        // retencja: usuwanie starych wysłanych; nowe (jeszcze niewysłane) wiadomości do indeksu nie trafiają
        builder.HasIndex(o => o.ProcessedAtUtc)
            .HasFilter($"{sql.Quote("ProcessedAtUtc")} IS NOT NULL");
    }
}
