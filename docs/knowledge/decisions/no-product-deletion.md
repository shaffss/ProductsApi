---
type: Decision
title: Produkty nie są usuwane
description: "Usuwanie produktów jest wyłączone w całym systemie (decyzja biznesowa) - kod zakomentowany, a DELETE /api/products/{id} zwraca 405."
resource: ../../../src/Products.Api/Endpoints/ProductEndpoints.cs
tags: [api, frontend, business-rule]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-17T10:00:00Z }
---

# Decyzja

Produktów nie można usuwać - ani przez API, ani z gridu. To decyzja biznesowa z 2026-10-17: usuwanie dodane wcześniej razem z listą okazało się niezgodne z wymaganiami.

# Konsekwencje

- **API nie ma endpointu `DELETE /api/products/{id}`.** Trasa `/api/products/{id}` istnieje tylko dla `GET`, więc odpowiedź to `405` z nagłówkiem `Allow: GET`. OpenAPI i Swagger nie pokazują `DELETE`. Pilnują tego test end-to-end we wszystkich wariantach i żądanie w Postmanie (folder 4).
- **Kod jest zakomentowany, nie usunięty.** Dotyczy to:
  - endpointu, `ProductService.DeleteAsync` i `IProductRepository.DeleteAsync` z implementacjami EF i Dappera;
  - SQL w `SqlDialect`;
  - przycisku, okna potwierdzenia i skrótu Delete w gridzie;
  - testów.

  Każde miejsce oznacza komentarz „wyłączone (decyzja biznesowa)”.
- **Przywrócenie:**
  - odkomentować te miejsca (`grep -rn "wyłączone (decyzja biznesowa)" src tests`);
  - przywrócić żądania `DELETE` w Postmanie z historii gita, bo JSON nie ma komentarzy;
  - zaktualizować opis w [przepływie](../flows/list-and-delete.md).
- **Schemat bazy bez zmian.** Klucze obce bez kaskady i tak nie pozwalają usunąć produktu z historią cen bez usunięcia wierszy zależnych.
- **Benchmarki** dalej usuwają z bazy własne produkty testowe (`cleanup`). To sprzątanie danych pomiarowych, a nie funkcja systemu.

Powiązane: [Lista i grid produktów](../flows/list-and-delete.md), [Kontrakt HTTP](../contracts/http-api.md)
