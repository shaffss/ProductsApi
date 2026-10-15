---
type: Flow
title: Utworzenie produktu
description: "POST /api/products: walidacja, opcjonalny Idempotency-Key, zapis w bazie i odpowiedź 201 z ETag."
resource: ../../../src/Products.Api/Endpoints/ProductEndpoints.cs
tags: [api, products, idempotency]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Kroki

1. Endpoint `Create` wiąże body `CreateProductRequest` (pola nullable - brak pola to błąd walidacji, a nie po cichu `0`) i nagłówek `Idempotency-Key` (cudzysłów wokół wartości jest zdejmowany).
2. `ProductService.CreateAsync` sprawdza wszystkie pola naraz - [Walidacja i błędy](../mechanisms/validation.md). Błędy: `400` z listą pól.
3. Bez klucza: `IProductRepository.CreateAsync` - `INSERT` zwraca wiersz z wartościami nadanymi przez bazę: Id (`NEWSEQUENTIALID()` / `uuidv7()`), daty i `Version = 1`.
4. Z kluczem: [Idempotency-Key](../mechanisms/idempotency-key.md) - produkt i klucz w jednej transakcji; ponowienie dostaje pierwszy produkt z `Idempotent-Replayed: true`, ten sam klucz z inną treścią - `422`.
5. Odpowiedź `201 Created`: `Location: /api/products/{id}`, `ETag: "1"` ([optymistyczna współbieżność](../mechanisms/optimistic-concurrency.md)) i produkt w body.

Utworzony produkt nie trafia do cache - zapisze go tam pierwszy [odczyt](read-product.md).
