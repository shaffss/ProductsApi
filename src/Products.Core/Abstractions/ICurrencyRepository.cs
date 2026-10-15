using Products.Core.Entities;

namespace Products.Core.Abstractions;

public interface ICurrencyRepository
{
    Task<IReadOnlyList<Currency>> GetActiveAsync(CancellationToken ct);
}
