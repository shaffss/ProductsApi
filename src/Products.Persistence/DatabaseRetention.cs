using System.Data.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Products.Persistence;

// Usuwa stare dane techniczne: wysłane wiadomości outboxa, obsłużone zdarzenia i klucze idempotencji.
// Paczkami (krótkie transakcje, bez długich blokad); kilka instancji naraz jest bezpieczne - usuwanie jest idempotentne.
public sealed partial class DatabaseRetention(
    DbDataSource dataSource, SqlDialect sql, RetentionOptions options, TimeProvider time, ILogger<DatabaseRetention> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Interval, time);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await DeleteAsync("OutboxMessages", sql.DeleteProcessedOutboxSql, now - options.OutboxMessages, ct);
        await DeleteAsync("ProcessedEvents", sql.DeleteProcessedEventsSql, now - options.ProcessedEvents, ct);
        await DeleteAsync("IdempotencyKeys", sql.DeleteIdempotencyKeysSql, now - options.IdempotencyKeys, ct);
    }

    private async Task DeleteAsync(string table, string deleteSql, DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        int deleted;
        do
        {
            await using var command = dataSource.CreateCommand(deleteSql);
            command.Parameters.AddRange(sql.DeleteExpiredParameters(cutoff, options.BatchSize));
            total += deleted = await command.ExecuteNonQueryAsync(ct);
        }
        while (deleted == options.BatchSize);

        if (total > 0)
            LogDeleted(logger, total, table);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention: deleted {Count} rows from {Table}")]
    private static partial void LogDeleted(ILogger logger, int count, string table);

    [LoggerMessage(Level = LogLevel.Error, Message = "Retention failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
