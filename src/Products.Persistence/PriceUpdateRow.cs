using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Persistence;

// Wynik SqlDialect.ChangePriceSql (wspólny dla EF i Dappera): produkt po operacji + poprzednia cena (null = bez zmian)
public sealed record PriceUpdateRow(
    Guid Id,
    string Name,
    decimal Price,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int Version,
    decimal? OldPrice,
    string? OldCurrency)
{
    // Bez zmiany i z inną wersją niż oczekiwana: UPDATE pominięty przez warunek wersji (If-Match)
    public PriceUpdate ToPriceUpdate(int? expectedVersion) => new(
        new Product
        {
            Id = Id,
            Name = Name,
            Price = Price,
            Currency = Currency,
            CreatedAtUtc = AsUtc(CreatedAtUtc),
            UpdatedAtUtc = AsUtc(UpdatedAtUtc),
            Version = Version,
        },
        OldPrice,
        OldCurrency,
        VersionConflict: OldPrice is null && expectedVersion is { } expected && Version != expected);

    // datetime2 z SQL Server nie niesie strefy
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
