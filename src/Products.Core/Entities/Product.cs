namespace Products.Core.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public required string Currency { get; set; } // kod waluty (Currencies.Code)
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int Version { get; set; } // zwiększana przy każdej zmianie
}
