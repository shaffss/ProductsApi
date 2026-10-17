-- ===== 000_database.sql =====
-- SQL Server: baza Products
-- Skrypty są idempotentne - można je uruchamiać wielokrotnie, osobno lub scalone (merge_files.cmd).
-- Te same instrukcje wykonuje usługa sqlserver-init w docker-compose.yml - przy zmianie popraw oba miejsca.

IF DB_ID(N'Products') IS NULL
    CREATE DATABASE [Products];
GO

-- READ_COMMITTED_SNAPSHOT (jak MVCC w PostgreSQL): odczyty nie czekają na blokady zapisów i odwrotnie.
-- ROLLBACK IMMEDIATE przerywa inne sesje w tej bazie (zmiana wymaga wyłącznego dostępu).
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'Products' AND is_read_committed_snapshot_on = 0)
    ALTER DATABASE [Products] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- ===== 001_currencies.sql =====
USE [Products];
GO

-- Słownik walut
IF OBJECT_ID(N'dbo.Currencies', N'U') IS NULL
    CREATE TABLE dbo.Currencies
    (
        Id       smallint      NOT NULL IDENTITY CONSTRAINT PK_Currencies PRIMARY KEY,
        Code     char(3)       NOT NULL CONSTRAINT AK_Currencies_Code UNIQUE, -- ISO 4217
        Name     nvarchar(100) NOT NULL,
        IsActive bit           NOT NULL CONSTRAINT DF_Currencies_IsActive DEFAULT CAST(1 AS bit)
    );
GO

SET IDENTITY_INSERT dbo.Currencies ON;

INSERT dbo.Currencies (Id, Code, Name)
SELECT s.Id, s.Code, s.Name
FROM (VALUES (1, 'PLN', N'Polski złoty'),
             (2, 'EUR', N'Euro'),
             (3, 'USD', N'Dolar amerykański')) AS s (Id, Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Currencies c WHERE c.Code = s.Code);

SET IDENTITY_INSERT dbo.Currencies OFF;
GO

-- ===== 002_products.sql =====
USE [Products];
GO

-- Produkty
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
    CREATE TABLE dbo.Products
    (
        Id           uniqueidentifier NOT NULL
            CONSTRAINT DF_Products_Id DEFAULT NEWSEQUENTIALID(),
        Name         nvarchar(200)    NOT NULL,
        Price        decimal(18, 2)   NOT NULL,
        Currency     char(3)          NOT NULL,
        CreatedAtUtc datetime2(7)     NOT NULL
            CONSTRAINT DF_Products_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7)     NOT NULL
            CONSTRAINT DF_Products_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        Version      int              NOT NULL
            CONSTRAINT DF_Products_Version DEFAULT 1, -- zwiększana przy każdej zmianie

        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Products_Currencies_Currency FOREIGN KEY (Currency) REFERENCES dbo.Currencies (Code),
        CONSTRAINT CK_Products_Name_NotEmpty CHECK (TRIM(Name) <> ''),
        CONSTRAINT CK_Products_Price_Positive CHECK (Price > 0)
    );
GO

-- lista od najnowszych (GET /api/products) - indeks czytany wstecz, Id rozstrzyga równe daty
IF INDEXPROPERTY(OBJECT_ID(N'dbo.Products'), N'IX_Products_CreatedAtUtc_Id', N'IndexId') IS NULL
    CREATE INDEX IX_Products_CreatedAtUtc_Id
        ON dbo.Products (CreatedAtUtc, Id);
GO

-- ===== 003_product_price_history.sql =====
USE [Products];
GO

-- Historia zmian cen - zapisywana w tej samej transakcji co zmiana ceny
IF OBJECT_ID(N'dbo.ProductPriceHistory', N'U') IS NULL
    CREATE TABLE dbo.ProductPriceHistory
    (
        Id           bigint           NOT NULL IDENTITY CONSTRAINT PK_ProductPriceHistory PRIMARY KEY,
        ProductId    uniqueidentifier NOT NULL,
        OldPrice     decimal(18, 2)   NOT NULL,
        OldCurrency  char(3)          NOT NULL,
        NewPrice     decimal(18, 2)   NOT NULL,
        NewCurrency  char(3)          NOT NULL,
        ChangedAtUtc datetime2(7)     NOT NULL,

        CONSTRAINT FK_ProductPriceHistory_Products_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id)
    );
GO

IF INDEXPROPERTY(OBJECT_ID(N'dbo.ProductPriceHistory'), N'IX_ProductPriceHistory_ProductId_ChangedAtUtc', N'IndexId') IS NULL
    CREATE INDEX IX_ProductPriceHistory_ProductId_ChangedAtUtc
        ON dbo.ProductPriceHistory (ProductId, ChangedAtUtc);
GO

-- ===== 004_outbox_messages.sql =====
USE [Products];
GO

-- wymagane przez indeks filtrowany (sqlcmd domyślnie ma OFF)
SET QUOTED_IDENTIFIER ON;
GO

-- Transactional Outbox - zdarzenia czekające na wysyłkę do Kafki
IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
    CREATE TABLE dbo.OutboxMessages
    (
        Id             bigint         NOT NULL IDENTITY CONSTRAINT PK_OutboxMessages PRIMARY KEY, -- kolejność wysyłki
        Type           varchar(200)   NOT NULL, -- typ zdarzenia z wersją schematu (nagłówek Kafki event-type)
        Topic          varchar(200)   NOT NULL,
        MessageKey     varchar(100)   NOT NULL, -- klucz partycji Kafki (ProductId) - kolejność per produkt
        Payload        nvarchar(max)  NOT NULL, -- JSON zdarzenia
        TraceParent    varchar(55)    NULL,     -- W3C trace context żądania, które zapisało zdarzenie
        OccurredAtUtc  datetime2(7)   NOT NULL,
        ProcessedAtUtc datetime2(7)   NULL,
        Attempts       int            NOT NULL CONSTRAINT DF_OutboxMessages_Attempts DEFAULT 0, -- błędy trwałe; limit -> dead-letter
        LastError      nvarchar(2000) NULL
    );
GO

-- mały indeks tylko z nieprzetworzonymi wiadomościami
IF INDEXPROPERTY(OBJECT_ID(N'dbo.OutboxMessages'), N'IX_OutboxMessages_Pending', N'IndexId') IS NULL
    CREATE INDEX IX_OutboxMessages_Pending
        ON dbo.OutboxMessages (Id)
        WHERE ProcessedAtUtc IS NULL;
GO

-- retencja: usuwanie starych wysłanych; nowe (jeszcze niewysłane) wiadomości do indeksu nie trafiają
IF INDEXPROPERTY(OBJECT_ID(N'dbo.OutboxMessages'), N'IX_OutboxMessages_ProcessedAtUtc', N'IndexId') IS NULL
    CREATE INDEX IX_OutboxMessages_ProcessedAtUtc
        ON dbo.OutboxMessages (ProcessedAtUtc)
        WHERE ProcessedAtUtc IS NOT NULL;
GO

-- ===== 005_processed_events.sql =====
USE [Products];
GO

-- Idempotentny konsument (inbox) - zdarzenia już obsłużone przez danego odbiorcę (grupę konsumentów Kafki)
IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
    CREATE TABLE dbo.ProcessedEvents
    (
        Consumer       varchar(100)     NOT NULL,
        EventId        uniqueidentifier NOT NULL,
        ProcessedAtUtc datetime2(7)     NOT NULL,

        CONSTRAINT PK_ProcessedEvents PRIMARY KEY (Consumer, EventId)
    );
GO

-- retencja: usuwanie starych wpisów
IF INDEXPROPERTY(OBJECT_ID(N'dbo.ProcessedEvents'), N'IX_ProcessedEvents_ProcessedAtUtc', N'IndexId') IS NULL
    CREATE INDEX IX_ProcessedEvents_ProcessedAtUtc
        ON dbo.ProcessedEvents (ProcessedAtUtc);
GO

-- ===== 006_idempotency_keys.sql =====
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

-- ===== 007_ef_migrations_history.sql =====
USE [Products];
GO

-- Historia migracji EF Core: baza ze skryptów jest dla EF już zmigrowana, więc warianty mogą na zmianę używać
-- jednej bazy - schemat z migracji (Database:Schema=Migrations) albo z tych skryptów (Scripts).
-- Nowa migracja EF = skrypt z tą samą zmianą schematu + jej Id poniżej (sprawdza to test SchemaSourcesTests).
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    CREATE TABLE dbo.__EFMigrationsHistory
    (
        MigrationId    nvarchar(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
        ProductVersion nvarchar(32)  NOT NULL
    );
GO

INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT s.MigrationId, s.ProductVersion
FROM (VALUES (N'20261015185853_InitialCreate', N'10.0.13'),
             (N'20261016090102_ProductListIndex', N'10.0.13')) AS s (MigrationId, ProductVersion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = s.MigrationId);
GO

