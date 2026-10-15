namespace Products.Core.Abstractions;

// Idempotentny konsument: zdarzenie obsłużone raz przez danego odbiorcę, nawet gdy Kafka dostarczy je ponownie
public interface IProcessedEventStore
{
    // Rejestruje zdarzenie i wykonuje handle w jednej transakcji; false = odbiorca już je obsłużył (duplikat).
    // Wyjątek z handle wycofuje rejestrację - zdarzenie zostanie obsłużone ponownie.
    Task<bool> TryProcessAsync(string consumer, Guid eventId, Func<CancellationToken, Task> handle, CancellationToken ct);
}
