using System.Data.Common;
using Dapper;
using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Persistence.Dapper;

internal sealed class DapperCurrencyRepository(DbDataSource dataSource, SqlDialect sql) : ICurrencyRepository
{
    public Task<IReadOnlyList<Currency>> GetActiveAsync(CancellationToken ct) => DbRetry.ExecuteAsync<IReadOnlyList<Currency>>(async ct =>
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var currencies = await connection.QueryAsync<Currency>(new CommandDefinition(sql.GetActiveCurrenciesSql, cancellationToken: ct));
        return currencies.AsList();
    }, ct);
}
