using Products.Core.Entities;

namespace Products.Core.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetAsync(Guid id, CancellationToken ct);

    // Strona od najnowszych (CreatedAtUtc, potem Id - kolejność jednoznaczna) i liczba wszystkich produktów
    Task<ProductPage> ListAsync(int skip, int take, CancellationToken ct);

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

    // Usuwanie produktów wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty
    // // Jedna transakcja: produkt z historią cen i kluczami idempotencji; false - produkt nie istnieje
    // Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}
