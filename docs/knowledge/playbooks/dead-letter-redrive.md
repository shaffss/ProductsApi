---
type: Playbook
title: Ponowienie wiadomości z dead-letter
description: "Jak znaleźć wiadomości outboxa po limicie prób i wysłać je ponownie po usunięciu przyczyny."
resource: ../../../src/Products.Infrastructure/Messaging/OutboxDispatcher.cs
tags: [outbox, kafka, operations]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

Sygnały: log `Outbox message {MessageId} moved to dead letter after {Attempts} attempts: {Error}` i metryka `products.outbox.dead_lettered`.

1. **Znajdź wiadomości.** Limit to `Kafka:OutboxMaxAttempts`, domyślnie 10. W PostgreSQL nazwy są w cudzysłowach, np. `"OutboxMessages"`.

   ```sql
   SELECT Id, Type, MessageKey, Attempts, LastError, OccurredAtUtc
   FROM OutboxMessages
   WHERE ProcessedAtUtc IS NULL AND Attempts >= 10
   ORDER BY Id;
   ```

2. **Usuń przyczynę** wskazaną w `LastError`, np. przy `MsgSizeTooLarge` podnieś `message.max.bytes` brokera lub topicu albo zmniejsz wiadomość.
3. **Wyzeruj licznik.** Dispatcher wyśle wiadomości w najbliższej paczce, w kolejności `Id`:

   ```sql
   UPDATE OutboxMessages SET Attempts = 0
   WHERE ProcessedAtUtc IS NULL AND Attempts >= 10;
   ```

4. **Sprawdź efekt:** `ProcessedAtUtc` jest ustawione, a w logu konsumenta pojawia się `Product price changed: ...`.

Późniejsze zdarzenia tego samego produktu zostały już wysłane, więc ponowione zdarzenie dotrze po nich. Rozpoznaje to pole `version` w zdarzeniu; obecny konsument tylko unieważnia cache i loguje, więc kolejność nie ma dla niego znaczenia.

Powiązane: [Transactional Outbox z dead-letter](../mechanisms/outbox.md)
