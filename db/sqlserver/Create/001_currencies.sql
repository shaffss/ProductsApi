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
