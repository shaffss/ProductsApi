USE [Products];
GO

-- Klucze idempotencji POST /api/products (nagłówek Idempotency-Key) - ponowione żądanie dostaje utworzony już produkt
IF OBJECT_ID(N'dbo.IdempotencyKeys', N'U') IS NULL
    CREATE TABLE dbo.IdempotencyKeys
    (
        [Key]        varchar(100)     NOT NULL CONSTRAINT PK_IdempotencyKeys PRIMARY KEY,
        ProductId    uniqueidentifier NOT NULL,
        RequestHash  binary(32)       NOT NULL, -- SHA-256 treści żądania: ten sam klucz z inną treścią to błąd 422
        CreatedAtUtc datetime2(7)     NOT NULL
            CONSTRAINT DF_IdempotencyKeys_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_IdempotencyKeys_Products_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id)
    );
GO

-- retencja: klucze ważne ograniczony czas
IF INDEXPROPERTY(OBJECT_ID(N'dbo.IdempotencyKeys'), N'IX_IdempotencyKeys_CreatedAtUtc', N'IndexId') IS NULL
    CREATE INDEX IX_IdempotencyKeys_CreatedAtUtc
        ON dbo.IdempotencyKeys (CreatedAtUtc);
GO
