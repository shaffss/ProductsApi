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
