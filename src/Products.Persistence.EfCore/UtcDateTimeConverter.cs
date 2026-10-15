using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Products.Persistence.EfCore;

// Wszystkie daty w modelu są w UTC - przy odczycie oznaczamy je jako Utc
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
