-- Klucze idempotencji POST /api/products (nagłówek Idempotency-Key) - ponowione żądanie dostaje utworzony już produkt
CREATE TABLE IF NOT EXISTS "IdempotencyKeys"
(
    "Key"          varchar(100) NOT NULL,
    "ProductId"    uuid         NOT NULL,
    "RequestHash"  bytea        NOT NULL, -- SHA-256 treści żądania: ten sam klucz z inną treścią to błąd 422
    "CreatedAtUtc" timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT "PK_IdempotencyKeys" PRIMARY KEY ("Key"),
    CONSTRAINT "FK_IdempotencyKeys_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id")
);

-- retencja: klucze ważne ograniczony czas
CREATE INDEX IF NOT EXISTS "IX_IdempotencyKeys_CreatedAtUtc"
    ON "IdempotencyKeys" ("CreatedAtUtc");
