using System.Data;
using Npgsql;

namespace Products.Persistence;

public sealed partial class SqlDialect
{
    public static readonly SqlDialect Postgres = new()
    {
        Engine = DatabaseEngine.Postgres,
        Factory = NpgsqlFactory.Instance,

        NewGuidSql = "uuidv7()",
        UtcNowSql = "now()",
        CurrencyCodeType = "varchar(3)", // char(n) porównywany z parametrem text rzutuje kolumnę i wyłącza indeks
        QuoteOpen = "\"",
        QuoteClose = "\"",

        CurrencyDbType = DbType.String,
        DateTimeDbType = DbType.DateTime, // timestamptz (DbType.DateTime2 to timestamp bez strefy)

        GetProductSql = """
            SELECT "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version"
            FROM "Products"
            WHERE "Id" = @id
            """,

        // Strona i liczba wszystkich w jednym poleceniu (dwa wyniki); indeks (CreatedAtUtc, Id) czytany wstecz
        ListProductsSql = """
            SELECT "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version"
            FROM "Products"
            ORDER BY "CreatedAtUtc" DESC, "Id" DESC
            OFFSET @skip LIMIT @take;
            SELECT count(*) FROM "Products";
            """,

        InsertProductSql = """
            INSERT INTO "Products" ("Name", "Price", "Currency")
            VALUES (@name, @price, @currency)
            RETURNING "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version"
            """,

        // Produkt i klucz idempotencji jednym poleceniem. Klucz już użyty: błąd klucza głównego - polecenie
        // nie zapisuje niczego, także produktu.
        InsertProductWithKeySql = """
            WITH product AS (
                INSERT INTO "Products" ("Name", "Price", "Currency")
                VALUES (@name, @price, @currency)
                RETURNING "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version"
            ), idempotency_key AS (
                INSERT INTO "IdempotencyKeys" ("Key", "ProductId", "RequestHash")
                SELECT @key, "Id", @requestHash FROM product
            )
            SELECT "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version" FROM product
            """,

        GetIdempotencyKeySql = """
            SELECT k."Key", k."RequestHash",
                   p."Id", p."Name", p."Price", p."Currency", p."CreatedAtUtc", p."UpdatedAtUtc", p."Version"
            FROM "IdempotencyKeys" k
            JOIN "Products" p ON p."Id" = k."ProductId"
            WHERE k."Key" = @key
            """,

        // Atomowo: UPDATE blokuje wiersz i zwraca starą cenę (RETURNING old.*); historia w tym samym poleceniu.
        // Warunek "cena lub waluta inna" w samym UPDATE - brak zmiany nie zapisuje niczego; tak samo inna wersja
        // produktu niż @expectedVersion (If-Match).
        // Wynik: produkt po operacji + stara cena (NULL = bez zmian); brak wiersza = produkt nie istnieje.
        // Druga część UNION widzi migawkę sprzed UPDATE, ale używana jest tylko gdy UPDATE niczego nie zmienił.
        ChangePriceSql = """
            WITH changed AS (
                UPDATE "Products"
                SET "Price" = @price, "Currency" = @currency, "UpdatedAtUtc" = now(), "Version" = "Version" + 1
                WHERE "Id" = @id AND ("Price" <> @price OR "Currency" <> @currency)
                  AND (@expectedVersion IS NULL OR "Version" = @expectedVersion)
                RETURNING new."Id", new."Name", new."Price", new."Currency", new."CreatedAtUtc", new."UpdatedAtUtc",
                          new."Version", old."Price" AS "OldPrice", old."Currency" AS "OldCurrency"
            ), history AS (
                INSERT INTO "ProductPriceHistory" ("ProductId", "OldPrice", "OldCurrency", "NewPrice", "NewCurrency", "ChangedAtUtc")
                SELECT "Id", "OldPrice", "OldCurrency", "Price", "Currency", "UpdatedAtUtc" FROM changed
            )
            SELECT "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version", "OldPrice", "OldCurrency"
            FROM changed
            UNION ALL
            SELECT "Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version", NULL, NULL
            FROM "Products"
            WHERE "Id" = @id AND NOT EXISTS (SELECT 1 FROM changed)
            """,

        // Usuwanie produktów wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty
        // // Jedno polecenie (atomowe): klucze idempotencji, historia i produkt. Klucze obce bez kaskady
        // // sprawdzane są na końcu polecenia, gdy wiersze zależne są już usunięte. Brak wiersza = brak produktu.
        // DeleteProductSql = """
        //     WITH keys AS (
        //         DELETE FROM "IdempotencyKeys" WHERE "ProductId" = @id
        //     ), history AS (
        //         DELETE FROM "ProductPriceHistory" WHERE "ProductId" = @id
        //     )
        //     DELETE FROM "Products" WHERE "Id" = @id
        //     RETURNING "Id"
        //     """,

        InsertOutboxMessageSql = """
            INSERT INTO "OutboxMessages" ("Type", "Topic", "MessageKey", "Payload", "TraceParent", "OccurredAtUtc")
            VALUES (@type, @topic, @messageKey, @payload, @traceParent, @occurredAtUtc)
            """,

        GetActiveCurrenciesSql = """
            SELECT "Id", "Code", "Name", "IsActive"
            FROM "Currencies"
            WHERE "IsActive"
            """,

        // Blokada doradcza na czas transakcji, bez czekania; zwalniana przy commit/rollback i zerwaniu połączenia
        TryLockOutboxSql = "SELECT pg_try_advisory_xact_lock(hashtext('OutboxDispatcher'))",

        // MVCC: odczyt nie czeka na niezatwierdzone transakcje (ich wiersze trafią do kolejnej paczki).
        // Attempts >= @maxAttempts: dead-letter - pomijane do ręcznego wyzerowania licznika.
        GetPendingOutboxSql = """
            SELECT "Id", "Type", "Topic", "MessageKey", "Payload", "TraceParent", "OccurredAtUtc", "Attempts"
            FROM "OutboxMessages"
            WHERE "ProcessedAtUtc" IS NULL AND "Attempts" < @maxAttempts
            ORDER BY "Id"
            LIMIT @batchSize
            """,

        MarkOutboxProcessedSql = """
            UPDATE "OutboxMessages"
            SET "ProcessedAtUtc" = now()
            WHERE "Id" = ANY(@ids)
            """,

        MarkOutboxFailedSql = """
            UPDATE "OutboxMessages"
            SET "Attempts" = "Attempts" + @attempt, "LastError" = @error
            WHERE "Id" = @id
            """,

        // 0 wierszy = zdarzenie już obsłużone przez odbiorcę
        InsertProcessedEventSql = """
            INSERT INTO "ProcessedEvents" ("Consumer", "EventId", "ProcessedAtUtc")
            VALUES (@consumer, @eventId, now())
            ON CONFLICT ("Consumer", "EventId") DO NOTHING
            """,

        // Retencja paczkami - krótkie transakcje zamiast jednego długiego DELETE (DELETE nie ma LIMIT - podzapytanie).
        // Outbox: tylko wysłane (dead-letter zostaje).
        DeleteProcessedOutboxSql = """
            DELETE FROM "OutboxMessages"
            WHERE "Id" IN (SELECT "Id" FROM "OutboxMessages" WHERE "ProcessedAtUtc" < @cutoff LIMIT @batchSize)
            """,

        DeleteProcessedEventsSql = """
            DELETE FROM "ProcessedEvents"
            WHERE ("Consumer", "EventId") IN (SELECT "Consumer", "EventId" FROM "ProcessedEvents"
                                              WHERE "ProcessedAtUtc" < @cutoff LIMIT @batchSize)
            """,

        DeleteIdempotencyKeysSql = """
            DELETE FROM "IdempotencyKeys"
            WHERE "Key" IN (SELECT "Key" FROM "IdempotencyKeys" WHERE "CreatedAtUtc" < @cutoff LIMIT @batchSize)
            """,
    };
}
