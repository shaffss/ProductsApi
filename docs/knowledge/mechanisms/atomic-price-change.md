---
type: Mechanism
title: Atomowa zmiana ceny
description: "Jedno polecenie SQL zmienia cenę, zwraca poprzednią i zapisuje historię - równoczesne zmiany nie gubią się."
resource: ../../../src/Products.Persistence/SqlDialect.SqlServer.cs
tags: [sql, concurrency, ef-core, dapper]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Jak działa

- **SQL Server:** `UPDATE ... OUTPUT deleted.*, inserted.* INTO @changed`, potem `INSERT` historii z `@changed` i `SELECT` produktu ze starą ceną. Zmienna tabelaryczna jest konieczna - [OUTPUT INTO nie pisze do tabeli z kluczem obcym](../pitfalls/output-into-table-with-fk.md).
- **PostgreSQL 18:** CTE `UPDATE ... RETURNING old.*, new.*` i `INSERT` historii w tym samym poleceniu.
- **Warunek w samym `UPDATE`:** cena lub waluta inna oraz `@expectedVersion IS NULL OR Version = @expectedVersion`. Niespełniony - zero zmian, a wynik to aktualny produkt bez starej ceny.
- **Blokada wiersza:** `UPDATE` blokuje wiersz, więc równoczesne zmiany idą po kolei i każda widzi poprzednią. Test: 20 równoczesnych zmian daje ciągłą historię.
- **Wynik:** `PriceUpdateRow.ToPriceUpdate` mapuje wiersz na `PriceUpdate`:
  - `Changed` - jest stara cena;
  - `VersionConflict` - brak zmiany przy innej wersji niż oczekiwana.
- **Outbox:** repozytorium dopisuje wiadomość w tej samej transakcji, tylko przy faktycznej zmianie.

# Odrzucone alternatywy

- **Semafor w aplikacji:** nie działa przy kilku instancjach API.
- **Blokada bazodanowa** (`sp_getapplock`, advisory lock): nic nie daje ponad blokadę wiersza, którą i tak zakłada `UPDATE`.
- **Odczyt, potem zapis:** wymagałby blokady albo kontroli wersji, a i tak to dwa polecenia zamiast jednego.

# Warianty ORM

Ten sam SQL z `SqlDialect` wykonują EF Core (`SqlQueryRaw` w strategii ponawiania) i Dapper - [uczciwe porównanie wariantów](../decisions/fair-comparison.md).

Powiązane: [Zmiana ceny](../flows/change-price.md), [Outbox](outbox.md), [Optymistyczna współbieżność](optimistic-concurrency.md)
