using Products.Core.Entities;

namespace Products.Core.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetAsync(Guid id, CancellationToken ct);

    // Id, daty i Version nadaje baza; zwraca zapisany produkt
    Task<Product> CreateAsync(string name, decimal price, string currency, CancellationToken ct);

    // Jak CreateAsync, ale produkt i klucz idempotencji w jednej transakcji; null - klucz był już użyty (nic nie zapisano)
    Task<Product?> TryCreateAsync(string name, decimal price, string currency, IdempotencyKey idempotencyKey, CancellationToken ct);

    // Klucz idempotencji z produktem utworzonym przy jego pierwszym użyciu; null - klucz nieznany
    Task<IdempotencyKey?> GetIdempotencyKeyAsync(string key, CancellationToken ct);

    // Jedna transakcja: atomowy UPDATE (zwraca starą cenę) + wpis historii + wiadomość w outboxie.
    // Gdy cena i waluta są takie same albo produkt ma inną wersję niż expectedVersion - nic nie zapisuje
    // (PriceUpdate.Changed == false). null = produkt nie istnieje
    Task<PriceUpdate?> ChangePriceAsync(Guid id, decimal price, string currency, int? expectedVersion, CancellationToken ct);
}
