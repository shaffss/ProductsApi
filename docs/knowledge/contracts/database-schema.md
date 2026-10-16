---
type: Database Schema
title: Schemat bazy
description: "Tabele produktów, historii cen, walut i danych technicznych (outbox, inbox, klucze idempotencji): przeznaczenie i indeksy."
resource: ../../../src/Products.Migrations.SqlServer/Migrations/
tags: [database, schema]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

Kolumny i typy są w migracjach ([SQL Server](../../../src/Products.Migrations.SqlServer/Migrations/), [PostgreSQL](../../../src/Products.Migrations.Postgres/Migrations/)) i w skryptach [db/](../../../db/). Baza nazywa się `Products` w SQL Server i `products` w PostgreSQL.

| Tabela | Przeznaczenie | Klucze i indeksy |
|---|---|---|
| `Currencies` | słownik walut - [decyzja](../decisions/currency-dictionary.md) | PK `Id`, unikalny `Code` |
| `Products` | produkty; `Version` rośnie przy każdej zmianie | PK `Id` (sekwencyjny), FK `Currency` → `Currencies.Code`, CHECK: niepusta nazwa i cena > 0 |
| `ProductPriceHistory` | stara i nowa cena oraz waluta przy każdej zmianie | FK `ProductId`, indeks `(ProductId, ChangedAtUtc)` |
| `OutboxMessages` | zdarzenia do wysłania - [Outbox](../mechanisms/outbox.md) | PK `Id` (kolejność wysyłki); filtrowane: `IX_OutboxMessages_Pending` (niewysłane) i `IX_OutboxMessages_ProcessedAtUtc` (wysłane, retencja) |
| `ProcessedEvents` | zdarzenia obsłużone przez odbiorcę - [Inbox](../mechanisms/inbox.md) | PK `(Consumer, EventId)`, indeks `ProcessedAtUtc` (retencja) |
| `IdempotencyKeys` | klucze POST - [Idempotency-Key](../mechanisms/idempotency-key.md) | PK `Key`, FK `ProductId`, indeks `CreatedAtUtc` (retencja) |
| `__EFMigrationsHistory` | zastosowane migracje EF; zapisują je też skrypty `db/` - [decyzja](../decisions/migrations-source-of-truth.md) | PK `MigrationId` |

Indeksy są tylko jawne: automatyczny indeks na każdym kluczu obcym (np. `Products.Currency` z 3 wartościami) nie przyspiesza odczytów, a spowalnia każdy zapis.

Powiązane: [Migracje EF jako źródło prawdy schematu](../decisions/migrations-source-of-truth.md), [Zmiana schematu bazy](../playbooks/schema-change.md)
