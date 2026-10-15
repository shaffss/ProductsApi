using System.Data.Common;
using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Persistence;

internal sealed class OutboxStore(DbDataSource dataSource, SqlDialect sql) : IOutboxStore
{
    public async Task<int?> ProcessBatchAsync(
        int batchSize,
        int maxAttempts,
        Func<IReadOnlyList<OutboxMessage>, CancellationToken, Task<OutboxPublishResult>> publish,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // blokada tylko na czas tej transakcji: druga instancja dostaje false i czeka na kolejną turę
        if (!await connection.ScalarAsync<bool>(transaction, sql.TryLockOutboxSql, ct))
            return null;

        var batch = await ReadPendingAsync(connection, transaction, batchSize, maxAttempts, ct);
        if (batch.Count == 0)
            return 0;

        var result = await publish(batch, ct);

        // wiadomości już są w Kafce - zapis stanu bez anulowania, inaczej poszłyby ponownie (duplikaty)
        if (result.Published > 0)
            await connection.ExecuteAsync(transaction, sql.MarkOutboxProcessedSql,
                sql.MarkOutboxProcessedParameters(batch.Take(result.Published).Select(m => m.Id)), CancellationToken.None);
        if (result.Error is { } error)
            await connection.ExecuteAsync(transaction, sql.MarkOutboxFailedSql,
                sql.MarkOutboxFailedParameters(batch[result.Published].Id, error, result.Permanent), CancellationToken.None);

        await transaction.CommitAsync(CancellationToken.None);
        return result.Published;
    }

    private async Task<List<OutboxMessage>> ReadPendingAsync(
        DbConnection connection, DbTransaction transaction, int batchSize, int maxAttempts, CancellationToken ct)
    {
        await using var command = connection.CreateCommand(
            transaction, sql.GetPendingOutboxSql, sql.GetPendingOutboxParameters(batchSize, maxAttempts));
        await using var reader = await command.ExecuteReaderAsync(ct);

        var batch = new List<OutboxMessage>(batchSize);
        while (await reader.ReadAsync(ct))
            batch.Add(new OutboxMessage
            {
                Id = reader.GetInt64(0),
                Type = reader.GetString(1),
                Topic = reader.GetString(2),
                MessageKey = reader.GetString(3),
                Payload = reader.GetString(4),
                TraceParent = reader.IsDBNull(5) ? null : reader.GetString(5),
                OccurredAtUtc = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc), // datetime2 bez strefy
                Attempts = reader.GetInt32(7),
            });
        return batch;
    }
}
