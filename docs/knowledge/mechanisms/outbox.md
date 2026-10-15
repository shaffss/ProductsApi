---
type: Mechanism
title: Transactional Outbox z dead-letter
description: "Zdarzenia zapisywane w transakcji zmiany danych i wysyłane do Kafki w tle: kolejność per produkt, at-least-once, dead-letter po limicie prób."
resource: ../../../src/Products.Infrastructure/Messaging/OutboxDispatcher.cs
tags: [kafka, outbox, reliability]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Zapis

Wiersz `OutboxMessages` powstaje w transakcji zmiany ceny: `Type`, `Topic`, `MessageKey` (ProductId), `Payload` (JSON) i `TraceParent`. Zdarzenie nie zginie i nie wyprzedzi danych.

# Wysyłka (`OutboxDispatcher`)

- Pętla co `Kafka:OutboxPollInterval` (1 s); po pełnej paczce (`Kafka:OutboxBatchSize`, 100) kolejna idzie od razu.
- Paczka jest przetwarzana pod blokadą w bazie na czas transakcji (`sp_getapplock` / `pg_try_advisory_xact_lock`, bez czekania). Przy kilku instancjach wysyła jedna naraz, w kolejności `Id`.
- Cała paczka jest wysyłana równolegle (producent zachowuje kolejność w partycji), a potwierdzenia są sprawdzane po kolei. Potwierdzone od początku paczki są oznaczane jawną listą `Id`.
- Producent: `EnableIdempotence`, `MessageTimeoutMs` = 10 s. Nagłówki `event-type` i `traceparent` - [kontrakt zdarzenia](../contracts/product-price-changed-v1.md).
- Stan po wysłaniu jest zapisywany bez anulowania (`CancellationToken.None`), bo inaczej wysłane wiadomości poszłyby drugi raz.
- Topic jest tworzony przy starcie z `Kafka:TopicPartitions` (3) partycjami; inaczej broker utworzyłby go z jedną.
- Gwarancja at-least-once: duplikaty odfiltrowuje [inbox](inbox.md).

# Dead-letter

- Błąd samej wiadomości zwiększa `Attempts`: `MsgSizeTooLarge`, `InvalidMsg`, `InvalidMsgSize`, `RecordListTooLarge`, `InvalidRecord`, `InvalidTimestamp`.
- Prób nie zużywają:
  - błędy przejściowe - przy braku brokera librdkafka ponawia sama do `MessageTimeoutMs`, potem zgłasza `Local_MsgTimedOut`;
  - błędy konfiguracji, np. uprawnienia albo brak topicu.

  Dzięki temu awaria Kafki nie przenosi wszystkiego do dead-letter.
- Po `Kafka:OutboxMaxAttempts` (10) wiadomość jest pomijana (log `Error`, metryka `products.outbox.dead_lettered`), więc kolejne nie czekają na nią w nieskończoność.
- Retencja jej nie usuwa. Ponowna wysyłka: [Ponowienie dead-letter](../playbooks/dead-letter-redrive.md).

# Odczyt bez blokowania

Zapytanie o paczkę nie czeka na niezatwierdzone transakcje: w PostgreSQL dzięki MVCC, w SQL Server dzięki `READ_COMMITTED_SNAPSHOT` ([decyzja](../decisions/rcsi-not-readpast.md)). Kolejność zdarzeń jednego produktu zapewnia blokada wiersza w [atomowej zmianie ceny](atomic-price-change.md).

Powiązane: [Zmiana ceny](../flows/change-price.md), [Retencja](retention.md)
