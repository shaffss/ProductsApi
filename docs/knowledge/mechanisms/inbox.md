---
type: Mechanism
title: Idempotentny konsument (inbox)
description: "Konsument ProductPriceChanged obsługuje zdarzenie raz na odbiorcę: rejestracja w ProcessedEvents w transakcji obsługi, ograniczone ponowienia."
resource: ../../../src/Products.Infrastructure/Messaging/PriceChangedConsumer.cs
tags: [kafka, idempotency, reliability]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Obsługa wiadomości

1. **Typ:** nagłówek `event-type` inny niż `ProductPriceChanged.v1` oznacza pominięcie przed deserializacją. Brak nagłówka - wiadomość sprzed wersjonowania (v1). Niepoprawny JSON - pominięcie z logiem.
2. **Ślad:** span `product-price-changed process` z rodzicem z nagłówka `traceparent`.
3. **Rejestracja:** `ProcessedEventStore.TryProcessAsync` w jednej transakcji wstawia `(Consumer, EventId)` i wykonuje obsługę.
   - SQL Server: `NOT EXISTS` z `UPDLOCK, HOLDLOCK`.
   - PostgreSQL: `ON CONFLICT DO NOTHING`.
   - Zero wstawionych wierszy - duplikat, pominięcie.
4. **Obsługa:** drugie unieważnienie cache i log `Product price changed: ...` (z `oldCurrency` przy zmianie waluty). Nieudane unieważnienie rzuca wyjątek, który wycofuje rejestrację.
5. **Offset:** zapisywany dopiero po obsłużeniu (`EnableAutoOffsetStore=false` + `StoreOffset`) - at-least-once.

# Ponowienia

- `Kafka:ConsumerMaxAttempts` (5) prób z odstępami 1 s, 2 s, 4 s, 8 s - łącznie daleko poniżej `max.poll.interval.ms` (5 min).
- Potem zdarzenie jest pomijane z logiem `Error` zawierającym treść (do ręcznej obsługi), żeby nie blokować partycji.
- Wpis cache wygaśnie wtedy najpóźniej po TTL.

# Odbiorcy

`Consumer` to grupa konsumentów Kafki. W compose każdy wariant ma własną grupę, więc każdy loguje wszystkie zdarzenia, także z drugiej bazy. Dzięki kluczowi `(Consumer, EventId)` dwa warianty na jednej bazie nie „zjadają” sobie zdarzeń.

# Odporność pętli

Wyjątek poza obsługą (np. utracone połączenie z brokerem) tworzy konsumenta od nowa po 5 s - [nieobsłużony wyjątek w BackgroundService zatrzymałby API](../pitfalls/background-service-exceptions.md).

Powiązane: [Outbox](outbox.md), [Retencja](retention.md), [ProductPriceChanged.v1](../contracts/product-price-changed-v1.md)
