using Microsoft.EntityFrameworkCore;
using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Persistence.EfCore;

internal sealed class EfCurrencyRepository(IDbContextFactory<ProductsDbContext> factory) : ICurrencyRepository
{
    public async Task<IReadOnlyList<Currency>> GetActiveAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Currencies.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);
    }
}
