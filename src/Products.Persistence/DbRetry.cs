namespace Products.Persistence;

// Ponawianie przy błędach przejściowych - ta sama polityka w obu ORM: EF Core przez EnableRetryOnFailure,
// Dapper przez ExecuteAsync. Ponawiana jest cała operacja (własne połączenie i transakcja).
public static class DbRetry
{
    public const int MaxRetries = 3;

    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(1);

    // Pierwsze ponowienie od razu (np. nowe połączenie z puli po zerwanym), kolejne po MaxDelay - jak EF Core
    // z tymi ustawieniami
    public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        for (var retry = 0; ; retry++)
        {
            try
            {
                return await operation(ct);
            }
            catch (Exception e) when (retry < MaxRetries && DbErrors.IsTransient(e))
            {
                await Task.Delay(retry == 0 ? TimeSpan.Zero : MaxDelay, ct);
            }
        }
    }
}
