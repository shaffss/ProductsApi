using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Core.Entities;

namespace Products.Persistence.EfCore.Configurations;

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.HasKey(e => new { e.Consumer, e.EventId });
        builder.Property(e => e.Consumer).HasMaxLength(100).IsUnicode(false); // nazwa grupy konsumentów Kafki
        builder.Property(e => e.EventId).ValueGeneratedNever(); // Id pochodzi z odebranego zdarzenia
        builder.HasIndex(e => e.ProcessedAtUtc); // retencja: usuwanie starych wpisów
    }
}
