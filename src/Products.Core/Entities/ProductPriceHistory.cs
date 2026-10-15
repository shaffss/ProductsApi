namespace Products.Core.Entities;

public sealed class ProductPriceHistory
{
    public long Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal OldPrice { get; set; }
    public required string OldCurrency { get; set; }
    public decimal NewPrice { get; set; }
    public required string NewCurrency { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
