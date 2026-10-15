---
type: Mechanism
title: Ponawianie przy błędach przejściowych bazy
description: "Zerwane połączenie, deadlock i chwilowa niedostępność bazy są ponawiane do 3 razy - ta sama polityka w EF Core i Dapperze."
resource: ../../../src/Products.Persistence/DbRetry.cs
tags: [database, reliability, ef-core, dapper]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **EF Core:** `EnableRetryOnFailure(3, 1 s)`. Jawna transakcja w `ChangePriceAsync` musi działać w `CreateExecutionStrategy().ExecuteAsync`, a każda próba zaczyna od `ChangeTracker.Clear()`, żeby nie zostawić wiadomości outboxa z nieudanej próby.
- **Dapper:** każda operacja repozytorium w `DbRetry.ExecuteAsync`. Pierwsze ponowienie od razu (nowe połączenie z puli), kolejne po 1 s - tak samo jak EF z tymi ustawieniami.
- **Klasyfikacja** (`DbErrors.IsTransient`):
  - Npgsql klasyfikuje sam (`NpgsqlException.IsTransient`);
  - `SqlException.IsTransient` jest zawsze `false`, więc dla SQL Server jest lista numerów jak w EF Core: deadlock 1205, błędy połączenia, Azure SQL;
  - timeout `-2` nie jest ponawiany: polecenie mogło się wykonać, a ponowienie dokłada obciążenia przeciążonej bazie.
- **Ponawiana jest cała operacja**, z własnym połączeniem i transakcją. Po niepewnym commicie:
  - zmiana ceny jest bezpieczna - druga próba widzi już nową cenę i kończy się jako „bez zmian”;
  - utworzenie z [Idempotency-Key](idempotency-key.md) też - konflikt klucza oznacza powtórkę;
  - utworzenie bez klucza może dać duplikat; przed tym chroni właśnie klucz.
- Outbox, inbox i retencja nie używają `DbRetry` - ich pętle ponawiają same.

Powiązane: [Uczciwe porównanie wariantów](../decisions/fair-comparison.md)
