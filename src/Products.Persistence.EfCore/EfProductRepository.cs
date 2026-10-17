using Microsoft.EntityFrameworkCore;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Core.Events;

namespace Products.Persistence.EfCore;

internal sealed class EfProductRepository(IDbContextFactory<ProductsDbContext> factory, SqlDialect sql) : IProductRepository
{
    // Zapytanie kompilowane raz (pole instancji, nie statyczne: skompilowane zapytanie jest związane
    // z modelem jednego providera, a w jednym procesie mogą działać repozytoria dla obu baz)
    private readonly Func<ProductsDbContext, Guid, CancellationToken, Task<Product?>> _getProduct =
        EF.CompileAsyncQuery((ProductsDbContext db, Guid id, CancellationToken ct) =>
            db.Products.AsNoTracking().FirstOrDefault(p => p.Id == id));

    public async Task<Product?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await _getProduct(db, id, ct);
    }

    public async Task<ProductPage> ListAsync(int skip, int take, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await db.Products.AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return new(items, await db.Products.LongCountAsync(ct), skip, take);
    }

    public async Task<Product> CreateAsync(string name, decimal price, string currency, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var product = new Product { Name = name, Price = price, Currency = currency };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct); // Id, daty i Version wracają z bazy
        return product;
    }

    public async Task<Product?> TryCreateAsync(
        string name, decimal price, string currency, IdempotencyKey idempotencyKey, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // produkt i klucz w jednym SaveChanges (jedna transakcja); ProductId klucza EF uzupełni po wstawieniu produktu
        idempotencyKey.Product = new Product { Name = name, Price = price, Currency = currency };
        db.IdempotencyKeys.Add(idempotencyKey);
        try
        {
            await db.SaveChangesAsync(ct);
            return idempotencyKey.Product;
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e.InnerException))
        {
            return null; // klucz już użyty - transakcja SaveChanges wycofana razem z produktem
        }
    }

    public async Task<IdempotencyKey?> GetIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.IdempotencyKeys.AsNoTracking().Include(k => k.Product).FirstOrDefaultAsync(k => k.Key == key, ct);
    }

    public async Task<PriceUpdate?> ChangePriceAsync(Guid id, decimal price, string currency, int? expectedVersion, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // SaveChanges w jawnej transakcji domyślnie zakłada savepoint (PostgreSQL: SAVEPOINT + RELEASE, SQL Server:
        // SAVE TRANSACTION) - dodatkowe wymiany z bazą, zbędne, bo każdy błąd wycofuje całą transakcję
        db.Database.AutoSavepointsEnabled = false;

        // jawna transakcja w strategii ponawiania (EnableRetryOnFailure) - przy błędzie przejściowym powtarzana w całości
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear(); // wiadomość outboxa z nieudanej próby
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // ten sam atomowy SQL co w Dapperze - porównanie ORM nie zależy od sposobu blokowania wiersza
            var rows = await db.Database
                .SqlQueryRaw<PriceUpdateRow>(sql.ChangePriceSql, sql.ChangePriceParameters(id, price, currency, expectedVersion))
                .ToListAsync(ct);
            if (rows.Count == 0)
                return null; // brak produktu - transakcja wycofana przy zwolnieniu

            var update = rows[0].ToPriceUpdate(expectedVersion);
            if (update.Changed)
            {
                db.OutboxMessages.Add(ProductPriceChanged.From(update).ToOutboxMessage());
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return update;
        }, ct);
    }

    // Usuwanie produktów wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty
    // // Klucze obce bez kaskady - najpierw wiersze zależne; ExecuteDelete bez wczytywania encji
    // public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    // {
    //     await using var db = await factory.CreateDbContextAsync(ct);
    //     return await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
    //     {
    //         await using var transaction = await db.Database.BeginTransactionAsync(ct);
    //         await db.IdempotencyKeys.Where(k => k.ProductId == id).ExecuteDeleteAsync(ct);
    //         await db.ProductPriceHistory.Where(h => h.ProductId == id).ExecuteDeleteAsync(ct);
    //         var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    //         await transaction.CommitAsync(ct);
    //         return deleted > 0;
    //     }, ct);
    // }
}
