---
type: Mechanism
title: Obserwowalność i health checki
description: "OpenTelemetry (ślady przez outbox i Kafkę, metryki, logi) włączane adresem OTLP oraz /health/live i /health/ready z limitami czasu."
resource: ../../../src/Products.Api/Observability.cs
tags: [opentelemetry, health, operations]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# OpenTelemetry

- **Włączanie:** tylko z `OTEL_EXPORTER_OTLP_ENDPOINT`, bez niego zero narzutu ([uczciwe porównanie wariantów](../decisions/fair-comparison.md)). W compose i Development dane trafiają do Aspire Dashboard (http://localhost:18888).
- **Ślady:**
  - instrumentacje ASP.NET Core, SqlClient i Npgsql (widać faktyczny SQL obu ORM);
  - własne spany z `ProductsTelemetry`: `product-price-changed publish` (Producer) i `product-price-changed process` (Consumer).
- **Propagacja:** `Activity.Current` przy zapisie zdarzenia → kolumna `TraceParent` → span wysyłki → nagłówek Kafki `traceparent` → span obsługi. Zmiana ceny i jej obsługa przez wszystkie warianty to jeden ślad.
- **Sampler** `ParentBased(SkipRootDatabaseCalls)`: zapytania bez rodzica (pętla outboxa co sekundę, retencja, migracje) nie trafiają do śladów.
- **Metryki aplikacji** `products.*`:
  - `cache.lookups` (hit/miss);
  - `outbox.published`, `outbox.failed` (transient/permanent), `outbox.dead_lettered`, `outbox.delay` (s);
  - `events.consumed` (handled/duplicate/skipped/failed).

  Do tego metryki ASP.NET Core i `System.Runtime`.
- **Logi** przez `[LoggerMessage]`, skorelowane ze śladami.
- Instrumentacji EF Core i StackExchange.Redis nie ma - w czasie pisania były tylko w wersjach beta.

# Health checki

| Endpoint | Sprawdza | Odpowiedź |
|---|---|---|
| `/health/live` | nic - tylko czy proces odpowiada | `200`; awaria bazy nie restartuje instancji |
| `/health/ready`, `/health` | baza (wymagana, limit 5 s), Redis i Kafka (opcjonalne, 3 s) | `200` Healthy/Degraded, `503` bez bazy |

- Zwykłe endpointy zamiast `MapHealthChecks`, dzięki czemu są w OpenAPI ze schematem odpowiedzi.
- Pole `variant` pokazuje działający wariant.
- Treść błędów jest tylko w Development.

Powiązane: [Zmiana ceny](../flows/change-price.md)
