using Products.Core;

namespace Products.UnitTests;

public sealed class CurrencyCatalogTests : IDisposable
{
    private readonly FakeCurrencyRepository _repository = new();
    private readonly ManualTimeProvider _time = new();
    private readonly CurrencyCatalog _catalog;

    public CurrencyCatalogTests() => _catalog = new(_repository, _time);

    public void Dispose() => _catalog.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Active_currencies_are_recognized_and_unknown_are_not()
    {
        Assert.True(await _catalog.IsActiveAsync("PLN", Ct));
        Assert.True(await _catalog.IsActiveAsync("USD", Ct));
        Assert.False(await _catalog.IsActiveAsync("GBP", Ct));
    }

    [Fact]
    public async Task Currencies_are_loaded_once_within_ttl()
    {
        for (var i = 0; i < 10; i++)
            await _catalog.IsActiveAsync("EUR", Ct);
        _time.Advance(TimeSpan.FromMinutes(4));
        await _catalog.IsActiveAsync("EUR", Ct);

        Assert.Equal(1, _repository.Calls);
    }

    [Fact]
    public async Task Deactivated_currency_is_rejected_after_ttl()
    {
        Assert.True(await _catalog.IsActiveAsync("USD", Ct));

        _repository.Currencies.Single(c => c.Code == "USD").IsActive = false;
        Assert.True(await _catalog.IsActiveAsync("USD", Ct)); // jeszcze z pamięci

        _time.Advance(TimeSpan.FromMinutes(6));
        Assert.False(await _catalog.IsActiveAsync("USD", Ct));
        Assert.Equal(2, _repository.Calls);
    }
}
