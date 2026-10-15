---
type: Decision
title: Cztery warianty dostępu do danych
description: "EF Core albo Dapper, każdy dla PostgreSQL albo SQL Server, wybierane konfiguracją; reszta kodu wspólna, żeby benchmarki porównywały tylko ORM i bazę."
resource: ../../../src/Products.Api/ServiceCollectionExtensions.cs
tags: [architecture, benchmarks, ef-core, dapper]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Decyzja

`Persistence:Orm` (`EfCore` | `Dapper`) i `Persistence:Database` (`Postgres` | `SqlServer`) wybierają implementację `IProductRepository` i `ICurrencyRepository`. Walidacja, cache, outbox, Kafka i API są wspólne.

# Konsekwencje

- **Różnice między bazami** są w jednym miejscu: `SqlDialect` (`SqlDialect.SqlServer.cs`, `SqlDialect.Postgres.cs`) - SQL, typy parametrów i konfiguracja modelu EF.
- **Projekty:** `Products.Persistence` (wspólne: dialekt, outbox, inbox, retencja), `Products.Persistence.EfCore`, `Products.Persistence.Dapper`.
- **Schemat** jest jeden dla obu ORM, z migracji EF ([decyzja](migrations-source-of-truth.md)); migracje per baza są w osobnych projektach.
- **Compose** uruchamia 4 kontenery API (porty 8081-8084). Dwa warianty na jednej bazie współdzielą outbox, ale wysyła jeden naraz.
- **`/health`** pokazuje działający wariant w polu `variant`.
- **Lokalnie:** profile w `launchSettings.json` albo okno wyboru (profil `Launcher`) - [Uruchomienie lokalne](../playbooks/run-locally.md).

Powiązane: [Uczciwe porównanie wariantów](fair-comparison.md)
