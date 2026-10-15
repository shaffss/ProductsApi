---
type: Mechanism
title: Idempotency-Key
description: "Ponowiony POST z tym samym kluczem zwraca produkt utworzony za pierwszym razem; ten sam klucz z inną treścią to 422."
resource: ../../../src/Products.Core/ProductService.cs
tags: [api, idempotency, reliability]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Zasady

Zgodnie z draftem IETF „The Idempotency-Key HTTP Header Field”:

- Nagłówek jest opcjonalny. Wartość w cudzysłowie (jak w drafcie) albo bez; 1-100 widocznych znaków ASCII (kolumna `varchar`).
- Treść jest porównywana po normalizacji: SHA-256 z nazwy po `Trim`, ceny z 2 miejscami po przecinku i kodu waluty. `10` i `10.00` oraz `pln` i `PLN` to to samo żądanie.
- Ponowienie: `201` z tym samym produktem i nagłówkiem `Idempotent-Replayed: true`. Ten sam klucz z inną treścią: `422`.
- Klucz jest ważny przez `Retention:IdempotencyKeys` (1 dzień); potem [retencja](retention.md) go usuwa i ten sam klucz utworzy nowy produkt.
- Klucze są globalne - bez uwierzytelniania nie ma klienta, do którego można je przypisać.

# Zapis

1. Najpierw próba wstawienia, bo nowy klucz to typowy przypadek. Produkt i wiersz `IdempotencyKeys` powstają w jednej transakcji:
   - Dapper: jedno polecenie (zmienna tabelaryczna / CTE);
   - EF Core: nawigacja `IdempotencyKey.Product`, jeden `SaveChanges` wstawia oba wiersze.
2. Konflikt klucza głównego (SQL Server 2627/2601, PostgreSQL 23505 - `DbErrors.IsUniqueViolation`) wycofuje także produkt.
3. Wtedy odczyt klucza z produktem i porównanie skrótów. Jeśli klucz zniknął w międzyczasie (retencja), następuje kolejna próba wstawienia.

Równoległe żądania z tym samym kluczem tworzą jeden produkt (test: 10 równoczesnych w każdym wariancie).

Powiązane: [Utworzenie produktu](../flows/create-product.md), [Kontrakt HTTP](../contracts/http-api.md)
