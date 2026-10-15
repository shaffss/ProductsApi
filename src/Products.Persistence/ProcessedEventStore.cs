using System.Data.Common;
using Products.Core.Abstractions;

namespace Products.Persistence;

internal sealed class ProcessedEventStore(DbDataSource dataSource, SqlDialect sql) : IProcessedEventStore
{
    public async Task<bool> TryProcessAsync(string consumer, Guid eventId, Func<CancellationToken, Task> handle, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var registered = await connection.ExecuteAsync(
            transaction, sql.InsertProcessedEventSql, sql.InsertProcessedEventParameters(consumer, eventId), ct);
        if (registered == 0)
            return false; // już obsłużone

        await handle(ct); // wyjątek -> rollback przy zwolnieniu transakcji, zdarzenie zostanie obsłużone ponownie
        await transaction.CommitAsync(ct);
        return true;
    }
}
