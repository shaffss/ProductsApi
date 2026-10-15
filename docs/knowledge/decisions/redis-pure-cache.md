---
type: Decision
title: Redis jako czysty cache
description: "StackExchange.Redis bezpośrednio, bez zapisu na dysk i z limitem pamięci LRU - baza jest źródłem prawdy, Redis może zgubić wszystko."
resource: ../../../docker-compose.yml
tags: [redis, cache]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Bez `IDistributedCache`:** ta abstrakcja zapisuje hash z metadanymi. Bezpośrednie `GET`/`SET` z TTL jest prostsze i tańsze.
- **Bez persystencji** (`--save ""`, `--appendonly no`): po restarcie Redis jest pusty. Z persystencją przywracał wpisy, których unieważnienie nie doszło do skutku w czasie awarii.
- **Limit pamięci** `256mb` z `allkeys-lru`: pełny cache wypiera najdawniej używane klucze, zamiast odrzucać zapisy.
- **Awaria Redisa nie jest awarią API:** `/health` pokazuje `Degraded`, a odczyty idą do bazy - [Cache-aside](../mechanisms/cache-aside.md).
