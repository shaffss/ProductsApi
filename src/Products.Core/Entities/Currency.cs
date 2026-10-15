namespace Products.Core.Entities;

public sealed class Currency
{
    public short Id { get; set; }
    public required string Code { get; set; } // ISO 4217
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}
