using Products.Core.Entities;

namespace Products.Core.Abstractions;

// Wynik zmiany ceny: produkt po operacji i poprzednia cena.
// OldPrice == null: nic nie zapisano (brak historii i zdarzenia) - cena i waluta bez zmian
// albo VersionConflict: produkt ma inną wersję niż oczekiwana (If-Match), czyli zmienił się w międzyczasie.
public sealed record PriceUpdate(Product Product, decimal? OldPrice, string? OldCurrency, bool VersionConflict = false)
{
    public bool Changed => OldPrice is not null;
}
