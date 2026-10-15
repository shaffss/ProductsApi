---
type: Decision
title: Uczciwe porównanie wariantów
description: "Warianty różnią się tylko ORM i bazą: ten sam SQL zmiany ceny, te same sterowniki i typy parametrów, bez narzutów fałszujących pomiary."
resource: ../../../src/Products.Persistence/SqlDialect.cs
tags: [benchmarks, performance, ef-core, dapper]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Zasady

- **Ten sam SQL** [atomowej zmiany ceny](../mechanisms/atomic-price-change.md) w EF (`SqlQueryRaw`) i Dapperze - wynik nie zależy od sposobu blokowania wiersza.
- **Te same sterowniki:** `Microsoft.Data.SqlClient` i `Npgsql` w wersjach, których używają providery EF.
- **Typy parametrów dopasowane do kolumn** (`SqlDialect`):
  - stała precyzja `decimal(18,2)` - SqlClient wyliczałby ją z wartości, a każda inna deklaracja parametru to osobny plan w cache SQL Server;
  - `datetime2` zamiast `datetime`;
  - `char(3)` jako `AnsiStringFixedLength`.
- **EF bez zbędnego narzutu:**
  - pula kontekstów i zapytanie kompilowane raz (pole instancji, bo model jest per provider);
  - bez automatycznych indeksów na kluczach obcych;
  - logi zapytań EF na `Warning` (na `Information` koszt byłby tylko w wariantach EF).
- **Wspólne i poza porównaniem:** outbox, inbox i retencja działają przez ADO.NET - praca w tle, ta sama dla obu ORM.
- **Ta sama semantyka baz:** `READ_COMMITTED_SNAPSHOT` w SQL Server, jak MVCC w PostgreSQL ([decyzja](rcsi-not-readpast.md)).
- **Ta sama polityka [ponawiania](../mechanisms/db-retries.md)** w obu ORM.
- **Bez scalania i bez telemetrii w pomiarach:**
  - przy `Cache:Enabled=false` każdy odczyt idzie do bazy (ochrona przed stampede wyłączona);
  - OpenTelemetry działa tylko z `OTEL_EXPORTER_OTLP_ENDPOINT`.
- **Bez alokacji:** JSON z source generatora, logi przez `[LoggerMessage]` (nic nie kosztują przy wyłączonym poziomie).

# Ustawienia do pomiarów

`Cache:Enabled=false`, `Kafka:Enabled=false`, pusty `OTEL_EXPORTER_OTLP_ENDPOINT`; dane z `db/*/Benchmark` - [Dane demo i do benchmarków](../playbooks/load-data.md).

Powiązane: [Cztery warianty](four-variants.md)
