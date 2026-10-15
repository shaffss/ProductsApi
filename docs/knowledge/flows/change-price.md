---
type: Flow
title: Zmiana ceny
description: "PUT /api/products/{id}/price: warunek If-Match, atomowy zapis z outboxem, zdarzenie w Kafce i log konsumenta."
resource: ../../../src/Products.Api/Endpoints/ProductEndpoints.cs
tags: [api, outbox, kafka, cache]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Kroki

1. Walidacja id, nagłówka `If-Match` (jeden silny ETag, np. `"3"`, albo `*`; inny format - `400`) oraz ceny i waluty - [Walidacja i błędy](../mechanisms/validation.md).
2. [Atomowa zmiana ceny](../mechanisms/atomic-price-change.md): w jednej transakcji `UPDATE` zwracający starą cenę, wpis historii i wiadomość w outboxie. Ta sama cena i waluta albo inna wersja niż w `If-Match` - nic nie jest zapisywane.
3. Po zatwierdzeniu: usunięcie `product:{id}` z Redisa ([Cache-aside](../mechanisms/cache-aside.md)).
4. Odpowiedź z produktem i aktualnym `ETag`:
   - `200` po zmianie;
   - `200` z polem `message`, gdy cena i waluta są takie same;
   - `412`, gdy produkt ma inną wersję niż w `If-Match` ([optymistyczna współbieżność](../mechanisms/optimistic-concurrency.md));
   - `404`, gdy produkt nie istnieje.
5. W tle [outbox](../mechanisms/outbox.md) wysyła [ProductPriceChanged.v1](../contracts/product-price-changed-v1.md) do Kafki, z kluczem ProductId.
6. [Inbox](../mechanisms/inbox.md): konsument każdego wariantu rejestruje zdarzenie w `ProcessedEvents`, drugi raz usuwa wpis cache i zapisuje log wymagany w zadaniu:

   ```
   Product price changed: productId=..., oldPrice=149.99, newPrice=129.99, currency=PLN
   ```

Cały przepływ, od żądania do konsumentów wszystkich wariantów, jest jednym śladem OpenTelemetry - [obserwowalność](../mechanisms/observability.md).
