using Products.Core.Entities;

namespace Products.Persistence.Dapper;

internal static class ProductExtensions
{
    // datetime2 z SQL Server wraca z Kind=Unspecified (w JSON bez "Z"); timestamptz z Postgresa już jako Utc
    public static Product WithUtcDates(this Product product)
    {
        product.CreatedAtUtc = DateTime.SpecifyKind(product.CreatedAtUtc, DateTimeKind.Utc);
        product.UpdatedAtUtc = DateTime.SpecifyKind(product.UpdatedAtUtc, DateTimeKind.Utc);
        return product;
    }
}
