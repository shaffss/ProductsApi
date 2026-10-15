---
type: Mechanism
title: Cache-aside z ochroną przed stampede
description: "Redis jako cache odczytu produktu: klucz product:{id} z TTL, podwójne unieważnienie i jeden odczyt z bazy przy równoczesnych żądaniach."
resource: ../../../src/Products.Infrastructure/Caching/RedisProductCache.cs
tags: [redis, cache, performance]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Odczyt

- `GET product:{id}`, wartość to JSON produktu (serializacja z source generatora). Trafienie - bez zapytania do bazy.
- Brak: odczyt z bazy i `SET` z TTL `Cache:ProductTtl` (5 min) jako fire-and-forget - odpowiedź nie czeka na Redisa.
- Wpis w innym formacie (`JsonException`, np. po zmianie klasy `Product`) jest traktowany jak brak i nadpisany odczytem z bazy.

# Cache stampede

Równoczesne żądania o produkt spoza cache czekają na jeden odczyt z bazy (`ConcurrentDictionary<Guid, Lazy<Task<Product?>>>` w `ProductService`).

- Odczyt jest wspólny, więc nie dostaje `CancellationToken` żądania: anulowanie jednego żądania nie przerywa pozostałych.
- Działa w obrębie instancji.
- Przy `Cache:Enabled=false` jest wyłączony (`IProductCache.IsEnabled`), żeby nie scalać zapytań w benchmarkach bazy.

# Unieważnienie

1. W `PUT`, po zatwierdzeniu zmiany w bazie.
2. Ponownie w konsumencie zdarzenia ([Inbox](inbox.md)) - naprawia przypadek, gdy Redis był niedostępny w chwili zmiany.

# Niedostępny Redis

- `AbortOnConnectFail=false`: API startuje bez Redisa.
- `BacklogPolicy.FailFast`: polecenie od razu kończy się błędem, zamiast czekać do 5 s.
- Odczyt idzie wtedy do bazy, a `/health` pokazuje `Degraded`.

# Znane ograniczenie

Odczyt z bazy sprzed zmiany ceny może zapisać do cache starą wartość już po unieważnieniu. Drugie unieważnienie zwykle to naprawia; w najgorszym razie nieaktualność trwa do końca TTL. Pełne rozwiązanie: zapis warunkowy po `Version` (skrypt Lua).

Powiązane: [Odczyt produktu](../flows/read-product.md), [Redis jako czysty cache](../decisions/redis-pure-cache.md)
