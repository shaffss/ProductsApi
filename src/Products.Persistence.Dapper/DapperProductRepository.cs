using System.Data.Common;
using Dapper;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Core.Events;

namespace Products.Persistence.Dapper;

// Każda operacja w DbRetry - odpowiednik EnableRetryOnFailure z wariantu EF Core
internal sealed class DapperProductRepository(DbDataSource dataSource, SqlDialect sql) : IProductRepository
{
    public Task<Product?> GetAsync(Guid id, CancellationToken ct) => DbRetry.ExecuteAsync(async ct =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        // First, nie Single: wyszukiwanie po kluczu głównym, bez sprawdzania drugiego wiersza (jak FirstOrDefault w EF)
        var product = await connection.QueryFirstOrDefaultAsync<Product>(
            new CommandDefinition(sql.GetProductSql, new DbParameters(sql.GetProductParameters(id)), cancellationToken: ct));
        return product?.WithUtcDates();
    }, ct);

    public Task<Product> CreateAsync(string name, decimal price, string currency, CancellationToken ct) => DbRetry.ExecuteAsync(async ct =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var product = await connection.QuerySingleAsync<Product>(new CommandDefinition(
            sql.InsertProductSql, new DbParameters(sql.InsertProductParameters(name, price, currency)), cancellationToken: ct));
        return product.WithUtcDates();
    }, ct);

    public Task<Product?> TryCreateAsync(
        string name, decimal price, string currency, IdempotencyKey idempotencyKey, CancellationToken ct) => DbRetry.ExecuteAsync(async ct =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var product = await connection.QuerySingleAsync<Product>(new CommandDefinition(sql.InsertProductWithKeySql,
                new DbParameters(sql.InsertProductWithKeyParameters(name, price, currency, idempotencyKey)), transaction, cancellationToken: ct));
            await transaction.CommitAsync(ct);
            return product.WithUtcDates();
        }
        catch (DbException e) when (DbErrors.IsUniqueViolation(e))
        {
            return null; // klucz już użyty - produkt wycofany razem z transakcją
        }
    }, ct);

    public Task<IdempotencyKey?> GetIdempotencyKeyAsync(string key, CancellationToken ct) => DbRetry.ExecuteAsync(async ct =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var keys = await connection.QueryAsync<IdempotencyKey, Product, IdempotencyKey>(
            new CommandDefinition(sql.GetIdempotencyKeySql, new DbParameters(sql.GetIdempotencyKeyParameters(key)), cancellationToken: ct),
            (idempotencyKey, product) =>
            {
                idempotencyKey.ProductId = product.Id;
                idempotencyKey.Product = product.WithUtcDates();
                return idempotencyKey;
            },
            splitOn: "Id");
        return keys.FirstOrDefault();
    }, ct);

    public Task<PriceUpdate?> ChangePriceAsync(
        Guid id, decimal price, string currency, int? expectedVersion, CancellationToken ct) => DbRetry.ExecuteAsync(async ct =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<PriceUpdateRow>(new CommandDefinition(sql.ChangePriceSql,
            new DbParameters(sql.ChangePriceParameters(id, price, currency, expectedVersion)), transaction, cancellationToken: ct));
        if (row is null)
            return null; // brak produktu - transakcja wycofana przy zwolnieniu

        var update = row.ToPriceUpdate(expectedVersion);
        if (update.Changed)
        {
            var message = ProductPriceChanged.From(update).ToOutboxMessage();
            await connection.ExecuteAsync(new CommandDefinition(
                sql.InsertOutboxMessageSql, new DbParameters(sql.InsertOutboxMessageParameters(message)), transaction, cancellationToken: ct));
        }
        await transaction.CommitAsync(ct);
        return update;
    }, ct);
}
