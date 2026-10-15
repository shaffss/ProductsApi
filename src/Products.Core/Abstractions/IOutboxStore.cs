using Products.Core.Entities;

namespace Products.Core.Abstractions;

// Transactional Outbox: paczka oczekujących wiadomości wysyłana pod blokadą bazodanową,
// więc wysyła tylko jedna instancja aplikacji naraz (zachowana kolejność).
public interface IOutboxStore
{
    // Zwraca liczbę wysłanych wiadomości; null, gdy blokadę trzyma inna instancja.
    // Wiadomości z maxAttempts nieudanych prób (dead-letter) są pomijane.
    Task<int?> ProcessBatchAsync(
        int batchSize,
        int maxAttempts,
        Func<IReadOnlyList<OutboxMessage>, CancellationToken, Task<OutboxPublishResult>> publish,
        CancellationToken ct);
}

// Published: ile pierwszych wiadomości paczki potwierdził broker; Error: przyczyna niepowodzenia następnej.
// Permanent: błąd samej wiadomości (np. za duża) - zużywa próbę; przejściowe (brak brokera, timeout) prób nie zużywają.
public readonly record struct OutboxPublishResult(int Published, string? Error, bool Permanent = false);
