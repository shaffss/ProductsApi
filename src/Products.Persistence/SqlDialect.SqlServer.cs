using System.Data;
using Microsoft.Data.SqlClient;

namespace Products.Persistence;

public sealed partial class SqlDialect
{
    public static readonly SqlDialect SqlServer = new()
    {
        Engine = DatabaseEngine.SqlServer,
        Factory = SqlClientFactory.Instance,

        NewGuidSql = "NEWSEQUENTIALID()",
        UtcNowSql = "SYSUTCDATETIME()",
        CurrencyCodeType = "char(3)", // stała długość, bez narzutu varchar
        QuoteOpen = "[",
        QuoteClose = "]",

        CurrencyDbType = DbType.AnsiStringFixedLength, // char(3)
        DateTimeDbType = DbType.DateTime2, // domyślne DbType.DateTime to typ datetime (dokładność 3 ms)

        GetProductSql = """
            SELECT Id, Name, Price, Currency, CreatedAtUtc, UpdatedAtUtc, Version
            FROM dbo.Products
            WHERE Id = @id
            """,

        InsertProductSql = """
            INSERT dbo.Products (Name, Price, Currency)
            OUTPUT inserted.Id, inserted.Name, inserted.Price, inserted.Currency,
                   inserted.CreatedAtUtc, inserted.UpdatedAtUtc, inserted.Version
            VALUES (@name, @price, @currency)
            """,

        // Produkt i klucz idempotencji jednym poleceniem (w transakcji wywołującego). Klucz już użyty: błąd klucza
        // głównego - wywołujący wycofuje transakcję razem z produktem.
        InsertProductWithKeySql = """
            DECLARE @product TABLE (Id uniqueidentifier, Name nvarchar(200), Price decimal(18, 2), Currency char(3),
                                    CreatedAtUtc datetime2, UpdatedAtUtc datetime2, Version int);

            INSERT dbo.Products (Name, Price, Currency)
            OUTPUT inserted.Id, inserted.Name, inserted.Price, inserted.Currency,
                   inserted.CreatedAtUtc, inserted.UpdatedAtUtc, inserted.Version
            INTO @product
            VALUES (@name, @price, @currency);

            INSERT dbo.IdempotencyKeys ([Key], ProductId, RequestHash)
            SELECT @key, Id, @requestHash FROM @product;

            SELECT Id, Name, Price, Currency, CreatedAtUtc, UpdatedAtUtc, Version FROM @product;
            """,

        GetIdempotencyKeySql = """
            SELECT k.[Key], k.RequestHash,
                   p.Id, p.Name, p.Price, p.Currency, p.CreatedAtUtc, p.UpdatedAtUtc, p.Version
            FROM dbo.IdempotencyKeys k
            JOIN dbo.Products p ON p.Id = k.ProductId
            WHERE k.[Key] = @key
            """,

        // Atomowo: UPDATE blokuje wiersz i zwraca starą cenę (OUTPUT deleted); historia w tej samej transakcji.
        // Warunek "cena lub waluta inna" w samym UPDATE - brak zmiany nie zapisuje niczego; tak samo inna wersja
        // produktu niż @expectedVersion (If-Match).
        // Zmienna tabelaryczna, bo OUTPUT bezpośrednio do INSERT nie działa dla tabeli z kluczem obcym.
        // Wynik: produkt po operacji + stara cena (NULL = bez zmian); brak wiersza = produkt nie istnieje.
        ChangePriceSql = """
            DECLARE @changed TABLE (OldPrice decimal(18, 2), OldCurrency char(3), NewPrice decimal(18, 2),
                                    NewCurrency char(3), ChangedAtUtc datetime2);

            UPDATE dbo.Products
            SET Price = @price, Currency = @currency, UpdatedAtUtc = SYSUTCDATETIME(), Version = Version + 1
            OUTPUT deleted.Price, deleted.Currency, inserted.Price, inserted.Currency, inserted.UpdatedAtUtc
            INTO @changed
            WHERE Id = @id AND (Price <> @price OR Currency <> @currency)
              AND (@expectedVersion IS NULL OR Version = @expectedVersion);

            INSERT dbo.ProductPriceHistory (ProductId, OldPrice, OldCurrency, NewPrice, NewCurrency, ChangedAtUtc)
            SELECT @id, OldPrice, OldCurrency, NewPrice, NewCurrency, ChangedAtUtc FROM @changed;

            SELECT p.Id, p.Name, p.Price, p.Currency, p.CreatedAtUtc, p.UpdatedAtUtc, p.Version, c.OldPrice, c.OldCurrency
            FROM dbo.Products p
            LEFT JOIN @changed c ON 1 = 1
            WHERE p.Id = @id;
            """,

        InsertOutboxMessageSql = """
            INSERT dbo.OutboxMessages (Type, Topic, MessageKey, Payload, TraceParent, OccurredAtUtc)
            VALUES (@type, @topic, @messageKey, @payload, @traceParent, @occurredAtUtc)
            """,

        GetActiveCurrenciesSql = """
            SELECT Id, Code, Name, IsActive
            FROM dbo.Currencies
            WHERE IsActive = 1
            """,

        // Blokada aplikacyjna na czas transakcji, bez czekania; zwalniana przy commit/rollback i zerwaniu połączenia
        TryLockOutboxSql = """
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = N'OutboxDispatcher', @LockMode = 'Exclusive',
                                         @LockOwner = 'Transaction', @LockTimeout = 0;
            SELECT CAST(IIF(@result >= 0, 1, 0) AS bit);
            """,

        // READ_COMMITTED_SNAPSHOT: odczyt nie czeka na niezatwierdzone transakcje (ich wiersze trafią do kolejnej paczki).
        // Attempts >= @maxAttempts: dead-letter - pomijane do ręcznego wyzerowania licznika.
        GetPendingOutboxSql = """
            SELECT TOP (@batchSize) Id, Type, Topic, MessageKey, Payload, TraceParent, OccurredAtUtc, Attempts
            FROM dbo.OutboxMessages
            WHERE ProcessedAtUtc IS NULL AND Attempts < @maxAttempts
            ORDER BY Id
            """,

        // Jawna lista Id (nie zakres): wiersze niezatwierdzonych transakcji mogą mieć Id ze środka zakresu
        MarkOutboxProcessedSql = """
            UPDATE dbo.OutboxMessages
            SET ProcessedAtUtc = SYSUTCDATETIME()
            WHERE Id IN (SELECT CAST(value AS bigint) FROM STRING_SPLIT(@ids, ','))
            """,

        MarkOutboxFailedSql = """
            UPDATE dbo.OutboxMessages
            SET Attempts = Attempts + @attempt, LastError = @error
            WHERE Id = @id
            """,

        // 0 wierszy = zdarzenie już obsłużone przez odbiorcę; UPDLOCK/HOLDLOCK blokuje klucz do końca transakcji
        InsertProcessedEventSql = """
            INSERT dbo.ProcessedEvents (Consumer, EventId, ProcessedAtUtc)
            SELECT @consumer, @eventId, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ProcessedEvents WITH (UPDLOCK, HOLDLOCK)
                              WHERE Consumer = @consumer AND EventId = @eventId)
            """,

        // Retencja paczkami - krótkie transakcje zamiast jednego długiego DELETE blokującego tabelę.
        // Outbox: tylko wysłane (dead-letter zostaje).
        DeleteProcessedOutboxSql = """
            DELETE TOP (@batchSize) FROM dbo.OutboxMessages
            WHERE ProcessedAtUtc < @cutoff
            """,

        DeleteProcessedEventsSql = """
            DELETE TOP (@batchSize) FROM dbo.ProcessedEvents
            WHERE ProcessedAtUtc < @cutoff
            """,

        DeleteIdempotencyKeysSql = """
            DELETE TOP (@batchSize) FROM dbo.IdempotencyKeys
            WHERE CreatedAtUtc < @cutoff
            """,
    };
}
