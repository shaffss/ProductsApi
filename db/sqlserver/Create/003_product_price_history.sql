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
