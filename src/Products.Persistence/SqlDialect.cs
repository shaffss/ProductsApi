using System.Data;
using System.Data.Common;
using Products.Core.Entities;

namespace Products.Persistence;

// Różnice między bazami w jednym miejscu: konfiguracja modelu EF, SQL i typy parametrów.
// Instancje: SqlDialect.SqlServer.cs, SqlDialect.Postgres.cs.
public sealed partial class SqlDialect
{
    public required DatabaseEngine Engine { get; init; }
    public required DbProviderFactory Factory { get; init; }

    // Model EF
    public required string NewGuidSql { get; init; }
    public required string UtcNowSql { get; init; }
    public required string CurrencyCodeType { get; init; }
    public required string QuoteOpen { get; init; }
    public required string QuoteClose { get; init; }

    // Typy parametrów - dobrane do kolumn, żeby uniknąć niejawnych konwersji
    public required DbType CurrencyDbType { get; init; }
    public required DbType DateTimeDbType { get; init; }

    // SQL (Dapper; ChangePriceSql także EF)
    public required string GetProductSql { get; init; }
    public required string InsertProductSql { get; init; }
    public required string InsertProductWithKeySql { get; init; }
    public required string GetIdempotencyKeySql { get; init; }
    public required string ChangePriceSql { get; init; }
    public required string InsertOutboxMessageSql { get; init; }
    public required string GetActiveCurrenciesSql { get; init; }

    // SQL wysyłki outboxa, idempotentnego konsumenta i retencji (ADO.NET, niezależnie od wariantu ORM)
    public required string TryLockOutboxSql { get; init; }
    public required string GetPendingOutboxSql { get; init; }
    public required string MarkOutboxProcessedSql { get; init; }
    public required string MarkOutboxFailedSql { get; init; }
    public required string InsertProcessedEventSql { get; init; }
    public required string DeleteProcessedOutboxSql { get; init; }
    public required string DeleteProcessedEventsSql { get; init; }
    public required string DeleteIdempotencyKeysSql { get; init; }

    public static SqlDialect For(DatabaseEngine engine) => engine == DatabaseEngine.SqlServer ? SqlServer : Postgres;

    public string Quote(string identifier) => QuoteOpen + identifier + QuoteClose;

    public DbParameter[] GetProductParameters(Guid id) => [Parameter("id", DbType.Guid, id)];

    public DbParameter[] InsertProductParameters(string name, decimal price, string currency) =>
        [Parameter("name", DbType.String, name, size: 200), Price(price), Currency(currency)];

    public DbParameter[] InsertProductWithKeyParameters(string name, decimal price, string currency, IdempotencyKey key) =>
        [.. InsertProductParameters(name, price, currency), Key(key.Key), Parameter("requestHash", DbType.Binary, key.RequestHash, size: 32)];

    public DbParameter[] GetIdempotencyKeyParameters(string key) => [Key(key)];

    // expectedVersion (If-Match): zmiana tylko przy tej wersji produktu; null - bez warunku
    public DbParameter[] ChangePriceParameters(Guid id, decimal price, string currency, int? expectedVersion) =>
        [Parameter("id", DbType.Guid, id), Price(price), Currency(currency), Parameter("expectedVersion", DbType.Int32, expectedVersion)];

    public DbParameter[] InsertOutboxMessageParameters(OutboxMessage message) =>
    [
        Parameter("type", DbType.AnsiString, message.Type, size: 200),
        Parameter("topic", DbType.AnsiString, message.Topic, size: 200),
        Parameter("messageKey", DbType.AnsiString, message.MessageKey, size: 100),
        Parameter("payload", DbType.String, message.Payload, size: -1), // nvarchar(max) / text
        Parameter("traceParent", DbType.AnsiString, message.TraceParent, size: 55),
        Parameter("occurredAtUtc", DateTimeDbType, message.OccurredAtUtc),
    ];

    public DbParameter[] GetPendingOutboxParameters(int batchSize, int maxAttempts) =>
        [Parameter("batchSize", DbType.Int32, batchSize), Parameter("maxAttempts", DbType.Int32, maxAttempts)];

    // SQL Server: lista "1,2,3" dla STRING_SPLIT; Postgres: tablica bigint[] dla = ANY(@ids)
    public DbParameter[] MarkOutboxProcessedParameters(IEnumerable<long> ids) =>
    [
        Engine == DatabaseEngine.SqlServer
            ? Parameter("ids", DbType.AnsiString, string.Join(',', ids), size: -1)
            : Parameter("ids", DbType.Object, ids.ToArray()),
    ];

    // permanent: błąd samej wiadomości - zużywa próbę (limit -> dead-letter); przejściowy tylko zapisuje przyczynę
    public DbParameter[] MarkOutboxFailedParameters(long id, string error, bool permanent) =>
    [
        Parameter("id", DbType.Int64, id),
        Parameter("error", DbType.String, error[..Math.Min(error.Length, 2000)], size: 2000),
        Parameter("attempt", DbType.Int32, permanent ? 1 : 0),
    ];

    public DbParameter[] InsertProcessedEventParameters(string consumer, Guid eventId) =>
        [Parameter("consumer", DbType.AnsiString, consumer, size: 100), Parameter("eventId", DbType.Guid, eventId)];

    public DbParameter[] DeleteExpiredParameters(DateTime cutoff, int batchSize) =>
        [Parameter("cutoff", DateTimeDbType, cutoff), Parameter("batchSize", DbType.Int32, batchSize)];

    // Stała precyzja: SqlClient wyliczałby ją z wartości (129.99 -> decimal(5,2), 1249.00 -> decimal(6,2)),
    // a każda inna deklaracja parametru to osobny plan w cache SQL Server.
    private DbParameter Price(decimal price)
    {
        var parameter = Parameter("price", DbType.Decimal, price);
        parameter.Precision = 18;
        parameter.Scale = 2;
        return parameter;
    }

    private DbParameter Currency(string currency) => Parameter("currency", CurrencyDbType, currency, size: 3);

    private DbParameter Key(string key) => Parameter("key", DbType.AnsiString, key, size: 100);

    private DbParameter Parameter(string name, DbType type, object? value, int size = 0)
    {
        var parameter = Factory.CreateParameter()!;
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        if (size != 0)
            parameter.Size = size;
        return parameter;
    }
}
