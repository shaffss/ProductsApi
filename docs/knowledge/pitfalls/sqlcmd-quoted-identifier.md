---
type: Pitfall
title: sqlcmd i QUOTED_IDENTIFIER
description: "sqlcmd domyślnie wyłącza QUOTED_IDENTIFIER, a indeksy filtrowane wymagają go przy tworzeniu i przy każdym zapisie do tabeli."
resource: ../../../db/sqlserver/Create/004_outbox_messages.sql
tags: [sql-server, sqlcmd, scripts]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Objaw:** `CREATE INDEX ... WHERE` albo `INSERT` / `UPDATE` na `OutboxMessages` uruchomione przez `sqlcmd` kończy się błędem „SET options have incorrect settings: 'QUOTED_IDENTIFIER'”.
- **Przyczyna:** `sqlcmd` ma `QUOTED_IDENTIFIER OFF`, a SqlClient (aplikacja, SSMS) - `ON`. Wymagają go indeksy filtrowane `IX_OutboxMessages_Pending` i `IX_OutboxMessages_ProcessedAtUtc`.
- **Rozwiązanie:** uruchamiaj `sqlcmd` z flagą `-I`. Skrypt `004_outbox_messages.sql` dodatkowo ustawia `SET QUOTED_IDENTIFIER ON` na początku.

Powiązane: [Dane demo i do benchmarków](../playbooks/load-data.md)
