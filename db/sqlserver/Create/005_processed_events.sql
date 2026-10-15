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
