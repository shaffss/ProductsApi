---
type: Flow
title: Odczyt produktu
description: "GET /api/products/{id}: cache-aside w Redisie, jeden odczyt z bazy przy równoczesnych żądaniach, nagłówki X-Cache i ETag."
resource: ../../../src/Products.Core/ProductService.cs
tags: [api, cache, redis]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Kroki

1. Id z trasy to string parsowany w handlerze: niepoprawny GUID daje `400` z `errors.id` ([kontrakt HTTP](../contracts/http-api.md)).
2. [Cache-aside](../mechanisms/cache-aside.md): `GET product:{id}` z Redisa. Trafienie - odpowiedź bez zapytania do bazy, `X-Cache: HIT`.
3. Brak w cache: jeden wspólny odczyt z bazy na produkt (ochrona przed cache stampede), zapis do Redisa z TTL 5 min bez czekania na potwierdzenie, `X-Cache: MISS`.
4. Brak produktu: `404` ProblemDetails; nieistniejący produkt nie trafia do cache.
5. `ETag: "{Version}"` - wersja do warunkowej [zmiany ceny](change-price.md) ([optymistyczna współbieżność](../mechanisms/optimistic-concurrency.md)).

Przy `Cache:Enabled=false` każdy odczyt idzie prosto do bazy, bez scalania równoczesnych żądań - [uczciwe porównanie wariantów](../decisions/fair-comparison.md).
