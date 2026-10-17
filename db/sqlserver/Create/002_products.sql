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
