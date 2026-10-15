namespace Products.Core.Entities;

// Klucz z nagłówka Idempotency-Key żądania POST /api/products - ponowione żądanie zwraca już utworzony produkt
public sealed class IdempotencyKey
{
    public required string Key { get; set; }
    public Guid ProductId { get; set; }
    public required byte[] RequestHash { get; set; } // SHA-256 treści żądania - ten sam klucz z inną treścią to błąd 422
    public DateTime CreatedAtUtc { get; set; }

    public Product? Product { get; set; } // tylko dla EF: Id produktu nadaje baza w tym samym zapisie
}
