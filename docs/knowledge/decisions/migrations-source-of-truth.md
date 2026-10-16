---
type: Decision
title: Migracje EF jako źródło prawdy schematu
description: "Schemat obu baz pochodzi z migracji EF Core; skrypty w db/ dają ten sam schemat i historię migracji (test), więc API może zakładać schemat jedną albo drugą drogą."
resource: ../../../src/Products.Migrations.SqlServer/Migrations/
tags: [database, migrations, schema]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Decyzja

- **Migracje per baza** w osobnych projektach (`Products.Migrations.SqlServer`, `Products.Migrations.Postgres`), każdy z `DesignTimeDbContextFactory` dla `dotnet ef`. Schemat jest wspólny dla EF i Dappera.
- **Jedna migracja `InitialCreate`**, dopóki projekt nie jest wdrożony; po wdrożeniu każda zmiana to nowa migracja.
- **Skrypty `db/<baza>/Create/NNN_*.sql`** (idempotentne) dają ten sam schemat bez EF. `db/merge_files.cmd` scala je do `db/<baza>/Publish/` dla `sqlcmd`/`psql`.
- **Dwie drogi przy starcie API** (`Database:Schema`):
  - `Migrations` - migracje EF;
  - `Scripts` - skrypty `Create/` wbudowane w `Products.Persistence`, bez EF;
  - `None` - schemat już jest.

  W compose warianty EF używają migracji, a warianty Dappera skryptów.
- **Zamienne na jednej bazie:**
  - skrypt `007_ef_migrations_history.sql` zapisuje w `__EFMigrationsHistory` Id migracji, które odtwarzają skrypty, więc EF uznaje taką bazę za zmigrowaną. Bez tego wpisu EF próbowałby tworzyć istniejące tabele;
  - skrypty uruchomione na bazie z migracji niczego nie zmieniają.
- **Parzystości pilnuje test `SchemaSourcesTests`:**
  - baza z migracji i baza ze skryptów mają ten sam zrzut katalogu ([zapytania](../references/schema-dump-sqlserver.sql)) i tę samą historię migracji;
  - obie drogi przełączane na jednej bazie niczego nie psują.

  Porównanie ręczne: [Zmiana schematu bazy](../playbooks/schema-change.md).
- **Blokada:** bazy zakładane są przed startem API (`POSTGRES_DB`, `sqlserver-init`), a schemat zakłada jedna instancja naraz (blokada migracji EF albo `sp_getapplock` / `pg_advisory_xact_lock` w skryptach) - [pułapka](../pitfalls/concurrent-database-creation.md). Na produkcji schemat powinien zakładać osobny krok wdrożenia (`dotnet ef migrations bundle` albo skrypty).

# Konwencje modelu

- **Indeksy tylko jawne** (`ForeignKeyIndexConvention` wyłączona).
- **Daty w UTC:** `datetime2` z konwerterem `Kind=Utc` w SQL Server, `timestamptz` w PostgreSQL - [pułapka](../pitfalls/datetime2-utc-kind.md).
- **Id nadaje baza:** `NEWSEQUENTIALID()` / `uuidv7()` - sekwencyjne, więc bez podziałów stron indeksu klastrowego.

Powiązane: [Schemat bazy](../contracts/database-schema.md)
