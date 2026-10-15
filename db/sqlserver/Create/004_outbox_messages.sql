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
