---
type: Decision
title: Słownik walut z kluczem po kodzie
description: "Waluty w tabeli Currencies (klucz Id, unikalny kod), produkty odwołują się do kodu ISO - odczyt bez złączenia, walidacja z pamięci."
resource: ../../../src/Products.Persistence.EfCore/Configurations/CurrencyConfiguration.cs
tags: [database, domain]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **`Currencies`:** `Id smallint` (klucz główny), `Code` (ISO 4217, klucz alternatywny), `Name`, `IsActive`. Dane początkowe: PLN, EUR, USD (`HasData` w migracji, `INSERT` w skrypcie).
- **`Products.Currency`** to klucz obcy do `Currencies.Code`. Kod jest w wierszu produktu, więc odczyt nie potrzebuje złączenia.
- **Typ kodu:** `char(3)` w SQL Server, `varchar(3)` w PostgreSQL. W PostgreSQL porównanie `char(n)` z parametrem `text` rzutuje kolumnę i wyłącza indeks.
- **`IsActive`** ma `HasDefaultValue(true)` razem z `HasSentinel(true)`: z domyślnym sentinelem `false` EF pomijałby kolumnę przy jawnym `false`, a baza wstawiałaby `true`.
- **Walidacja** korzysta z `CurrencyCatalog` w pamięci, odświeżanego co 5 min; zmiana `IsActive` działa najpóźniej po tym czasie - [Walidacja i błędy](../mechanisms/validation.md).

Powiązane: [Schemat bazy](../contracts/database-schema.md)
