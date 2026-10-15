---
type: Decision
title: Migracje EF jako źródło prawdy schematu
description: "Schemat obu baz pochodzi z migracji EF Core; skrypty w db/ dają identyczny schemat, sprawdzany porównaniem katalogów."
resource: ../../../src/Products.Migrations.SqlServer/Migrations/
tags: [database, migrations, schema]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Decyzja

- **Migracje per baza** w osobnych projektach (`Products.Migrations.SqlServer`, `Products.Migrations.Postgres`), każdy z `DesignTimeDbContextFactory` dla `dotnet ef`. Schemat jest wspólny dla EF i Dappera.
- **Jedna migracja `InitialCreate`**, dopóki projekt nie jest wdrożony; po wdrożeniu każda zmiana to nowa migracja.
- **Skrypty `db/<baza>/Create/NNN_*.sql`** (idempotentne) dla osób bez .NET; `db/merge_files.cmd` scala je do `db/<baza>/Publish/`.
- **Parzystość** migracji i skryptów jest sprawdzana porównaniem katalogów systemowych obu baz - [Zmiana schematu bazy](../playbooks/schema-change.md).
- **`Database:MigrateOnStartup=true` tylko w compose.** Bazy zakładane są przed startem API (`POSTGRES_DB`, `sqlserver-init`), a EF blokuje bazę na czas migracji, więc kilka instancji naraz jest bezpiecznych - [pułapka](../pitfalls/concurrent-database-creation.md). Na produkcji migracje powinien wykonywać osobny krok wdrożenia (`dotnet ef migrations bundle`).

# Konwencje modelu

- **Indeksy tylko jawne** (`ForeignKeyIndexConvention` wyłączona).
- **Daty w UTC:** `datetime2` z konwerterem `Kind=Utc` w SQL Server, `timestamptz` w PostgreSQL - [pułapka](../pitfalls/datetime2-utc-kind.md).
- **Id nadaje baza:** `NEWSEQUENTIALID()` / `uuidv7()` - sekwencyjne, więc bez podziałów stron indeksu klastrowego.

Powiązane: [Schemat bazy](../contracts/database-schema.md)
