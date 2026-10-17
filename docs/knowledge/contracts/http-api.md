---
type: API
title: Kontrakt HTTP
description: "Semantyka endpointów /api/products (lista, tworzenie, odczyt, zmiana ceny) i /health: statusy, nagłówki i zasady, których nie widać w schemacie OpenAPI."
resource: ../../../src/Products.Api/Endpoints/ProductEndpoints.cs
tags: [api, http, openapi]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

Schemat żądań i odpowiedzi jest w OpenAPI pod `/openapi/v1.json` (Swagger UI pod `/swagger`, w Development).

| Endpoint | Sukces | Błędy |
|---|---|---|
| `GET /api/products?skip&take` | `200` + `items`, `total`, `skip`, `take` (od najnowszych) | `400` `skip` < 0 albo `take` poza 1-100 |
| `POST /api/products` | `201` + `Location` + `ETag` | `400` walidacja; `422` klucz idempotencji użyty z inną treścią |
| `GET /api/products/{id}` | `200` + `ETag` + `X-Cache: HIT/MISS` | `400` id nie jest GUID; `404` |
| `PUT /api/products/{id}/price` | `200` + `ETag`; bez zmian - pole `message` | `400` walidacja, id albo `If-Match`; `404`; `412` nieaktualny `If-Match` |
| `DELETE /api/products/{id}` | - | `405` - usuwanie produktów [wyłączone](../decisions/no-product-deletion.md) |
| `GET /` | frontend (grid produktów) | - |
| `GET /health`, `/health/ready` | `200` Healthy/Degraded | `503` baza niedostępna |
| `GET /health/live` | `200` | - |

# Zasady

- **Błędy:** ProblemDetails (RFC 9457). `400` zawiera listę błędów per pole (`errors`); przy niepoprawnym JSON pole jest wskazane w `detail` (`$.price`).
- **Niepoprawne id to `400`, nie `404`:** id jest stringiem parsowanym w handlerze, a błąd trafia do `errors.id`. Ograniczenie trasy `{id:guid}` dawałoby `404`.
- **PUT z tą samą ceną i walutą** niczego nie zapisuje (brak historii i zdarzenia) i zwraca `200` z produktem oraz polem `message`.
- **PUT zawsze zwraca cały produkt** po operacji.
- **Lista** (stronicowanie, kolejność) oraz frontend: [Lista i grid produktów](../flows/list-and-delete.md).
- **Nagłówki:**
  - `Idempotency-Key` / `Idempotent-Replayed` - [Idempotency-Key](../mechanisms/idempotency-key.md);
  - `ETag` / `If-Match` - [optymistyczna współbieżność](../mechanisms/optimistic-concurrency.md);
  - `X-Cache` - [Cache-aside](../mechanisms/cache-aside.md).
- **Daty** w UTC z sufiksem `Z` (`updatedAtUtc`).
- **Cena** zawsze z 2 miejscami po przecinku, jak w bazie (`299.00`), w każdym wariancie. EF przy utworzeniu zwraca wartość z żądania, więc DTO normalizuje zapis.

Powiązane: [Walidacja i błędy](../mechanisms/validation.md), [Obserwowalność i health checki](../mechanisms/observability.md)
