-- Historia migracji EF Core: baza ze skryptów jest dla EF już zmigrowana, więc warianty mogą na zmianę używać
-- jednej bazy - schemat z migracji (Database:Schema=Migrations) albo z tych skryptów (Scripts).
-- Nowa migracja EF = skrypt z tą samą zmianą schematu + jej Id poniżej (sprawdza to test SchemaSourcesTests).
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory"
(
    "MigrationId"    varchar(150) NOT NULL,
    "ProductVersion" varchar(32)  NOT NULL,

    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015185859_InitialCreate', '10.0.13')
ON CONFLICT ("MigrationId") DO NOTHING;
