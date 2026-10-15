---
type: Mechanism
title: Walidacja i błędy
description: "Walidacja w ProductService zwraca Result<T> zamiast wyjątków; błędy jako ProblemDetails z listą pól, także dla niepoprawnego JSON."
resource: ../../../src/Products.Core/ProductService.cs
tags: [validation, api, problem-details]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Reguły

| Pole | Reguła |
|---|---|
| `name` | wymagane, spacje obcinane, maks. 200 znaków |
| `price` | wymagana, większa od 0, maks. 2 miejsca po przecinku, zakres `decimal(18,2)` |
| `currency` | aktywna w `Currencies` (PLN, EUR, USD), bez rozróżniania wielkości liter, zapis jako `PLN` |
| `Idempotency-Key` | 1-100 widocznych znaków ASCII |

Zwracane są wszystkie błędne pola naraz; słownik błędów powstaje dopiero przy pierwszym błędzie.

# Mechanizm

- **`Result<T>`** (`Value`, `Errors`): bez wyjątków na gorącej ścieżce. `Value == null` bez błędów to wynik szczególny opisany przy operacji (brak produktu, klucz idempotencji z inną treścią).
- **Waluty** z `CurrencyCatalog`: aktywne kody w pamięci (`FrozenSet`), odświeżane co 5 min, jedno zapytanie naraz ([słownik walut](../decisions/currency-dictionary.md)).
- **Pola DTO są nullable**, więc brak pola daje błąd „required”, a nie po cichu `0`.
- **Niepoprawny JSON i brak body:** `BadRequestExceptionHandler` z `ThrowOnBadRequest=true` zwraca `400` ze wskazaniem pola (`$.price`) w każdym środowisku. Bez tego Development dawał `500`.
- **Format błędów:** ProblemDetails (RFC 9457); komunikaty API są po angielsku.

Powiązane: [Kontrakt HTTP](../contracts/http-api.md)
