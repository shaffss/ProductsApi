using System.Collections.Frozen;
using Products.Core.Abstractions;

namespace Products.Core;

// Aktywne waluty w pamięci - walidacja waluty bez zapytania do bazy przy każdym żądaniu.
// Odświeżane leniwie po upływie TTL, więc zmiana IsActive dociera najpóźniej po tym czasie.
public sealed class CurrencyCatalog(ICurrencyRepository repository, TimeProvider time) : IDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1); // jedno odświeżenie naraz zamiast lawiny zapytań
    private volatile Snapshot? _snapshot;

    public async ValueTask<bool> IsActiveAsync(string code, CancellationToken ct) =>
        (await GetSnapshotAsync(ct)).Codes.Contains(code);

    private async ValueTask<Snapshot> GetSnapshotAsync(CancellationToken ct)
    {
        if (_snapshot is { } current && current.ExpiresAt > time.GetUtcNow())
            return current;

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_snapshot is { } refreshed && refreshed.ExpiresAt > time.GetUtcNow())
                return refreshed; // odświeżył inny wątek, gdy czekaliśmy

            var currencies = await repository.GetActiveAsync(ct);
            return _snapshot = new Snapshot(
                currencies.Select(c => c.Code).ToFrozenSet(StringComparer.Ordinal),
                time.GetUtcNow() + Ttl);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose() => _refreshLock.Dispose();

    private sealed record Snapshot(FrozenSet<string> Codes, DateTimeOffset ExpiresAt);
}
