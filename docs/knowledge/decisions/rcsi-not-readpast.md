---
type: Decision
title: READ_COMMITTED_SNAPSHOT zamiast READPAST
description: "Odczyt outboxa w SQL Server nie czeka na niezatwierdzone transakcje dzięki wersjonowaniu wierszy, a nie hintowi READPAST."
resource: ../../../db/sqlserver/Create/000_database.sql
tags: [sql-server, outbox, concurrency]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Decyzja

SQL Server działa z `READ_COMMITTED_SNAPSHOT ON`. Zapytanie o paczkę [outboxa](../mechanisms/outbox.md) nie ma hintu `READPAST`.

# Powody

- **Ta sama semantyka co MVCC w PostgreSQL:** odczyty nie czekają na blokady zapisów i odwrotnie, więc warianty można uczciwie porównać.
- **Bez blokowania odczytów:** odczyt produktu nie czeka na trwającą zmianę ceny - ma znaczenie w benchmarkach z równoczesnymi odczytami i zapisami.
- **`READPAST` jest niedozwolony** przy RCSI na poziomie READ COMMITTED (kończy się błędem), a migawka i tak pomija niezatwierdzone wiersze.

# Konsekwencje

- **EF Core** włącza RCSI sam, gdy tworzy bazę SQL Server (`dotnet ef database update`, migracje przy starcie). Skrypt `000_database.sql` robi to warunkowo.
- **Baza utworzona inaczej** wymaga ręcznego `ALTER DATABASE Products SET READ_COMMITTED_SNAPSHOT ON`. Bez tego wszystko działa poprawnie, tylko odczyt paczki czasem chwilę czeka na zatwierdzenie zmiany ceny.
- **ALTER nie jest w migracji:** EF ostrzega przed operacją poza transakcją migracji, a `ROLLBACK IMMEDIATE` przy równoległym starcie przerwałby migrację drugiej instancji.
- **Oznaczanie wysłanych** wiadomości używa jawnej listy `Id`, nie zakresu, bo niezatwierdzone transakcje mogą mieć `Id` ze środka zakresu.

Powiązane: [Uczciwe porównanie wariantów](fair-comparison.md)
