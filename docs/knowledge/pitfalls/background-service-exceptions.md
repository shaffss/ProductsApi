---
type: Pitfall
title: Wyjątki w BackgroundService i konsumencie Kafki
description: "Nieobsłużony wyjątek w BackgroundService zatrzymuje całe API, a StoreOffset po utracie partycji rzuca wyjątek."
resource: ../../../src/Products.Infrastructure/Messaging/PriceChangedConsumer.cs
tags: [hosting, kafka, reliability]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Zatrzymanie hosta:** od .NET 6 domyślne `BackgroundServiceExceptionBehavior.StopHost` sprawia, że wyjątek z `ExecuteAsync` zatrzymuje całą aplikację. Awaria konsumenta Kafki wyłączyłaby więc API.
  - Konsument ma pętlę restartu (`RunAsync` tworzy go od nowa po 5 s).
  - Dispatcher outboxa i retencja łapią wyjątki w każdym przebiegu pętli.
- **`StoreOffset` po rebalansie:** gdy partycja zostanie odebrana w trakcie obsługi (np. obsługa dłuższa niż `max.poll.interval.ms`), `StoreOffset` rzuca `KafkaException`. Dlatego jest w osobnym `try`: wiadomość wróci, a inbox ją pominie.
- **Blokowanie startu:** `Consume()` blokuje wątek, więc pętla konsumenta działa w `Task.Run`, a nie w wątku startu hosta.

Powiązane: [Idempotentny konsument (inbox)](../mechanisms/inbox.md)
