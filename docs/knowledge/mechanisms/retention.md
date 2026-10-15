---
type: Mechanism
title: Retencja danych technicznych
description: "Co godzinę usuwa paczkami wysłane wiadomości outboxa, obsłużone zdarzenia i stare klucze idempotencji; dead-letter zostaje."
resource: ../../../src/Products.Persistence/DatabaseRetention.cs
tags: [database, maintenance]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

| Tabela | Usuwane | Po | Dlaczego tyle |
|---|---|---|---|
| `OutboxMessages` | wysłane (`ProcessedAtUtc`) | 7 dniach | diagnostyka; dead-letter (`ProcessedAtUtc IS NULL`) zostaje |
| `ProcessedEvents` | obsłużone (`ProcessedAtUtc`) | 7 dniach | domyślna retencja topicu Kafki - starsze zdarzenie już nie wróci |
| `IdempotencyKeys` | utworzone (`CreatedAtUtc`) | 1 dniu | okno ponowień klienta |

- `BackgroundService` z `PeriodicTimer` co `Retention:Interval` (1 h); pierwsze przejście przy starcie.
- Paczki po `Retention:BatchSize` (1000), czyli krótkie transakcje:
  - SQL Server: `DELETE TOP (n)`;
  - PostgreSQL: `DELETE ... WHERE klucz IN (SELECT ... LIMIT n)`.
- Indeksy pod retencję:
  - `IX_OutboxMessages_ProcessedAtUtc` - filtrowany, tylko wysłane, więc nowe wiadomości nic nie kosztują;
  - `IX_ProcessedEvents_ProcessedAtUtc`;
  - `IX_IdempotencyKeys_CreatedAtUtc`.
- Kilka instancji naraz jest bezpiecznych, bo usuwanie jest idempotentne. `Retention:Enabled=false` wyłącza mechanizm.

Powiązane: [Outbox](outbox.md), [Inbox](inbox.md), [Idempotency-Key](idempotency-key.md)
