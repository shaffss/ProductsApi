using System.Data.Common;
using Products.Persistence;

namespace Products.IntegrationTests;

// Bezpośrednie zapytania do bazy w asercjach (stan po operacji, a nie to, co zwróciło repozytorium)
internal sealed class TestDatabase(DbDataSource dataSource, SqlDialect sql)
{
    public async Task<List<(decimal OldPrice, decimal NewPrice)>> GetHistoryAsync(Guid productId) =>
        await QueryAsync(
            $"SELECT {Q("OldPrice")}, {Q("NewPrice")} FROM {Q("ProductPriceHistory")} WHERE {Q("ProductId")} = @key ORDER BY {Q("Id")}",
            productId, r => (r.GetDecimal(0), r.GetDecimal(1)));

    public async Task<List<OutboxRow>> GetOutboxAsync(Guid productId) =>
        await QueryAsync(
            $"SELECT {Q("Type")}, {Q("Payload")}, {Q("TraceParent")}, {Q("ProcessedAtUtc")}, {Q("Attempts")}, {Q("LastError")} " +
            $"FROM {Q("OutboxMessages")} WHERE {Q("MessageKey")} = @key ORDER BY {Q("Id")}",
            productId.ToString(),
            r => new OutboxRow(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetDateTime(3), r.GetInt32(4), r.IsDBNull(5) ? null : r.GetString(5)));

    // przez dowolnego odbiorcę
    public async Task<bool> IsEventProcessedAsync(Guid eventId) =>
        (await QueryAsync($"SELECT 1 FROM {Q("ProcessedEvents")} WHERE {Q("EventId")} = @key", eventId, _ => 1)).Count > 0;

    public async Task<bool> IdempotencyKeyExistsAsync(string key) =>
        (await QueryAsync($"SELECT 1 FROM {Q("IdempotencyKeys")} WHERE {Q("Key")} = @key", key, _ => 1)).Count > 0;

    public async Task<int> CountProductsAsync(string name) =>
        (await QueryAsync($"SELECT {Q("Id")} FROM {Q("Products")} WHERE {Q("Name")} = @key", name, _ => 1)).Count;

    // Przestawia datę wierszy (np. starsze niż okres retencji): UPDATE {table} SET {column} = @value WHERE {keyColumn} = @key
    public async Task SetDateAsync(string table, string column, string keyColumn, object key, DateTime value)
    {
        await using var command = Command(
            $"UPDATE {Q(table)} SET {Q(column)} = @value WHERE {Q(keyColumn)} = @key", key);
        var parameter = command.CreateParameter();
        parameter.ParameterName = "value";
        parameter.DbType = sql.DateTimeDbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
        Assert.NotEqual(0, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    private string Q(string identifier) => sql.Quote(identifier);

    private async Task<List<T>> QueryAsync<T>(string commandText, object key, Func<DbDataReader, T> map)
    {
        await using var command = Command(commandText, key);
        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            rows.Add(map(reader));
        return rows;
    }

    private DbCommand Command(string commandText, object key)
    {
        var command = dataSource.CreateCommand(commandText);
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = key;
        command.Parameters.Add(parameter);
        return command;
    }
}

internal sealed record OutboxRow(
    string Type, string Payload, string? TraceParent, DateTime? ProcessedAtUtc, int Attempts, string? LastError);
