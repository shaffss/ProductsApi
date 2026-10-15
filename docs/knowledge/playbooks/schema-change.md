---
type: Playbook
title: Zmiana schematu bazy
description: "Zmiana modelu EF: migracje dla obu baz, skrypty db/, porównanie schematów i bazy deweloperskie."
resource: ../../../src/Products.Persistence.EfCore/ProductsDbContext.cs
tags: [database, migrations, schema]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Kroki

1. **Model:** zmień encję (`src/Products.Core/Entities`) i jej konfigurację (`src/Products.Persistence.EfCore/Configurations`). Różnice między bazami wyrażaj przez `SqlDialect`.
2. **Migracje dla obu baz.** Zawsze najpierw build, bo `dotnet ef ... --no-build` używa starego builda ([pułapka](../pitfalls/ef-tools-stale-build.md)):

   ```bash
   dotnet build src/Products.Migrations.SqlServer
   dotnet ef migrations add <Nazwa> --project src/Products.Migrations.SqlServer
   dotnet build src/Products.Migrations.Postgres
   dotnet ef migrations add <Nazwa> --project src/Products.Migrations.Postgres
   ```

   Kolumna `NOT NULL` dodana do istniejącej tabeli dostaje w migracji `DEFAULT ''` - ustaw sensowną wartość dla istniejących wierszy.
3. **Skrypty:** zaktualizuj `db/<baza>/Create/NNN_*.sql` (muszą zostać idempotentne) i scal je do `db/<baza>/Publish/` poleceniem `.\db\merge_files.cmd` (Windows).
4. **Porównanie schematów:** dla każdego silnika baza z migracji i baza ze skryptów, zrzut katalogu, `diff`. Polecenia uruchamiaj z katalogu głównego repozytorium (tam jest manifest `dotnet ef`); w Git Bash najpierw `export MSYS_NO_PATHCONV=1`.

   ```bash
   # SQL Server
   SQLCMD="docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P LocalDev_Passw0rd -C -b -I -W"
   sed "s/\[Products\]/[ProductsScript]/g; s/N'Products'/N'ProductsScript'/g" db/sqlserver/Publish/create.sql | $SQLCMD -i /dev/stdin
   dotnet ef database update --project src/Products.Migrations.SqlServer --connection "Server=localhost,1433;Database=ProductsEf;User Id=sa;Password=LocalDev_Passw0rd;TrustServerCertificate=True"
   for db in ProductsEf ProductsScript; do $SQLCMD -d $db -h -1 -i /dev/stdin < docs/knowledge/references/schema-dump-sqlserver.sql > /tmp/$db.txt; done
   diff /tmp/ProductsEf.txt /tmp/ProductsScript.txt && $SQLCMD -Q "DROP DATABASE ProductsEf; DROP DATABASE ProductsScript"

   # PostgreSQL
   PSQL="docker exec -i postgres psql -U postgres -v ON_ERROR_STOP=1 -q"
   $PSQL -c "CREATE DATABASE products_script"
   sed "s/products/products_script/g" db/postgres/Publish/create.sql | grep -v -e "^SELECT 'CREATE DATABASE" -e "^WHERE NOT EXISTS (SELECT 1 FROM pg_database" | $PSQL -d products_script
   dotnet ef database update --project src/Products.Migrations.Postgres --connection "Host=localhost;Port=5432;Database=products_ef;Username=postgres;Password=LocalDev_Passw0rd"
   for db in products_ef products_script; do $PSQL -d $db -At < docs/knowledge/references/schema-dump-postgres.sql > /tmp/$db.txt; done
   diff /tmp/products_ef.txt /tmp/products_script.txt && $PSQL -c "DROP DATABASE products_ef" -c "DROP DATABASE products_script"
   ```

   Zrzut obejmuje kolumny z typami i wartościami domyślnymi, klucze, klucze obce, CHECK, indeksy (z filtrami) i dane `Currencies`. Nazwy domyślnych constraintów SQL Server są pomijane, bo EF nadaje własne. Zapytania: [schema-dump-sqlserver.sql](../references/schema-dump-sqlserver.sql), [schema-dump-postgres.sql](../references/schema-dump-postgres.sql).
5. **Bazy deweloperskie:** kontenery API stosują migracje przy starcie (`docker compose up -d --build`); lokalnie `dotnet ef database update --project ...`.
6. **Testy:** przy zmianie SQL w `SqlDialect` testy integracyjne obejmują wszystkie warianty (`ProductRepositoryTests`, `MessagingStoreTests`, `ApiEndToEndTests`).

Powiązane: [Migracje EF jako źródło prawdy schematu](../decisions/migrations-source-of-truth.md), [Schemat bazy](../contracts/database-schema.md)
