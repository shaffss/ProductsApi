---
type: Pitfall
title: datetime2 bez strefy czasowej
description: "datetime2 z SQL Server wraca jako DateTimeKind.Unspecified (JSON bez Z) - daty trzeba oznaczać jako UTC przy odczycie."
resource: ../../../src/Products.Persistence.EfCore/UtcDateTimeConverter.cs
tags: [sql-server, dates, json]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Objaw:** w wariantach SQL Server daty w JSON nie mają sufiksu `Z`, w wariantach PostgreSQL mają.
- **Przyczyna:** `datetime2` nie przechowuje strefy, więc odczyt daje `Kind=Unspecified`. `timestamptz` z PostgreSQL wraca jako `Kind=Utc`.
- **Rozwiązanie:** oznaczanie przy odczycie, w każdej ścieżce:
  - EF Core: `UtcDateTimeConverter` na wszystkich `DateTime` (tylko SQL Server);
  - Dapper: `ProductExtensions.WithUtcDates`;
  - wspólny SQL: `PriceUpdateRow`, `OutboxStore`.
- **W drugą stronę:** Npgsql wymaga `Kind=Utc` dla parametrów `timestamptz`, dlatego daty w aplikacji zawsze są w UTC.

Powiązane: [Migracje EF jako źródło prawdy schematu](../decisions/migrations-source-of-truth.md)
