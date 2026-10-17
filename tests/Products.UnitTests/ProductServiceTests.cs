using Products.Core;

namespace Products.UnitTests;

public sealed class ProductServiceTests
{
    private readonly FakeProductRepository _repository = new();
    private readonly FakeProductCache _cache = new();
    private readonly ProductService _service;

    public ProductServiceTests() => _service = CreateService(_cache);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<decimal> NonPositivePrices => new() { 0m, -0.01m, -100m };

    public static TheoryData<string> InvalidIdempotencyKeys => new() { "", "klucz z odstępem", "żółw", new string('k', 101) };

    [Theory]
    [MemberData(nameof(NonPositivePrices))]
    public async Task Create_rejects_price_not_greater_than_zero(decimal price)
    {
        var result = await _service.CreateAsync("Czujnik temperatury", price, "PLN", null, Ct);

        Assert.NotNull(result.Errors);
        Assert.Equal(["Price must be greater than zero."], result.Errors["price"]);
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task Create_rejects_price_with_more_than_two_decimal_places()
    {
        var result = await _service.CreateAsync("Czujnik temperatury", 1.234m, "PLN", null, Ct);

        Assert.True(result.Errors?.ContainsKey("price"));
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task Create_reports_all_invalid_fields()
    {
        var result = await _service.CreateAsync("   ", 10m, "GBP", null, Ct);

        Assert.NotNull(result.Errors);
        Assert.Equal(["currency", "name"], result.Errors.Keys.Order());
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task Create_trims_name_and_normalizes_currency_code()
    {
        var result = await _service.CreateAsync("  Czujnik temperatury  ", 149.99m, "pln", null, Ct);

        Assert.Null(result.Errors);
        Assert.Equal("Czujnik temperatury", result.Value?.Product.Name);
        Assert.Equal("PLN", result.Value?.Product.Currency);
        Assert.False(result.Value?.Replayed);
    }

    [Fact]
    public async Task Create_retried_with_same_idempotency_key_returns_first_product()
    {
        var first = await _service.CreateAsync("Czujnik temperatury", 10m, "PLN", "order-1", Ct);
        // ta sama treść inaczej zapisana: spacje w nazwie, cena 10.00, waluta małymi literami
        var retry = await _service.CreateAsync(" Czujnik temperatury ", 10.00m, "pln", "order-1", Ct);

        Assert.False(first.Value?.Replayed);
        Assert.True(retry.Value?.Replayed);
        Assert.Equal(first.Value?.Product.Id, retry.Value?.Product.Id);
        Assert.Equal(1, _repository.CreateCalls);
    }

    [Fact]
    public async Task Create_with_idempotency_key_used_for_different_request_is_rejected()
    {
        await _service.CreateAsync("Czujnik temperatury", 10m, "PLN", "order-1", Ct);

        var result = await _service.CreateAsync("Czujnik temperatury", 11m, "PLN", "order-1", Ct);

        Assert.Null(result.Errors);
        Assert.Null(result.Value); // endpoint zwraca 422
        Assert.Equal(1, _repository.CreateCalls);
    }

    [Theory]
    [MemberData(nameof(InvalidIdempotencyKeys))]
    public async Task Create_rejects_invalid_idempotency_key(string key)
    {
        var result = await _service.CreateAsync("Czujnik temperatury", 10m, "PLN", key, Ct);

        Assert.True(result.Errors?.ContainsKey("Idempotency-Key"));
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task ChangePrice_of_missing_product_returns_not_found()
    {
        var result = await _service.ChangePriceAsync(Guid.NewGuid(), 129.99m, "PLN", null, Ct);

        Assert.Null(result.Errors);
        Assert.Null(result.Value); // endpoint zwraca 404
        Assert.Empty(_cache.Removed);
    }

    [Fact]
    public async Task ChangePrice_invalidates_cache_entry()
    {
        var product = _repository.Add(price: 149.99m);
        _cache.Entries[product.Id] = product;

        var result = await _service.ChangePriceAsync(product.Id, 129.99m, "PLN", null, Ct);

        Assert.True(result.Value?.Changed);
        Assert.Equal([product.Id], _cache.Removed);
        Assert.False(_cache.Entries.ContainsKey(product.Id));
    }

    [Fact]
    public async Task ChangePrice_with_same_price_and_currency_changes_nothing()
    {
        var product = _repository.Add(price: 149.99m, currency: "PLN");
        _cache.Entries[product.Id] = product;

        var result = await _service.ChangePriceAsync(product.Id, 149.99m, "pln", null, Ct);

        Assert.False(result.Value?.Changed);
        Assert.Equal(1, result.Value?.Product.Version);
        Assert.Empty(_cache.Removed);
    }

    [Fact]
    public async Task ChangePrice_with_stale_expected_version_reports_conflict_and_changes_nothing()
    {
        var product = _repository.Add(price: 149.99m);
        _cache.Entries[product.Id] = product;
        Assert.True((await _service.ChangePriceAsync(product.Id, 139.99m, "PLN", expectedVersion: 1, Ct)).Value?.Changed);

        // drugi klient z wersją sprzed pierwszej zmiany (ETag "1")
        var result = await _service.ChangePriceAsync(product.Id, 129.99m, "PLN", expectedVersion: 1, Ct);

        Assert.True(result.Value?.VersionConflict); // endpoint zwraca 412
        Assert.False(result.Value?.Changed);
        Assert.Equal((139.99m, 2), (product.Price, product.Version));
        Assert.Single(_cache.Removed); // tylko po pierwszej zmianie
    }

    [Fact]
    public async Task Get_returns_cached_product_without_querying_database()
    {
        var product = _repository.Add();
        _cache.Entries[product.Id] = product;

        var lookup = await _service.GetAsync(product.Id, Ct);

        Assert.True(lookup.FromCache);
        Assert.Same(product, lookup.Product);
        Assert.Equal(0, _repository.GetCalls);
    }

    [Fact]
    public async Task Get_on_cache_miss_loads_from_database_and_caches_product()
    {
        var product = _repository.Add();

        var first = await _service.GetAsync(product.Id, Ct);
        var second = await _service.GetAsync(product.Id, Ct);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, _repository.GetCalls);
    }

    [Fact]
    public async Task Concurrent_cache_misses_load_product_from_database_once()
    {
        var product = _repository.Add();
        _repository.GetGate = new TaskCompletionSource();

        var lookups = Enumerable.Range(0, 10).Select(_ => _service.GetAsync(product.Id, Ct)).ToArray();
        _repository.GetGate.SetResult();
        var results = await Task.WhenAll(lookups);

        Assert.Equal(1, _repository.GetCalls); // cache stampede: jedno zapytanie zamiast dziesięciu
        Assert.All(results, r => Assert.Same(product, r.Product));
        Assert.True((await _service.GetAsync(product.Id, Ct)).FromCache);
    }

    [Fact]
    public async Task Get_with_cache_disabled_always_reads_database()
    {
        var service = CreateService(new FakeProductCache { IsEnabled = false });
        var product = _repository.Add();

        await service.GetAsync(product.Id, Ct);
        var second = await service.GetAsync(product.Id, Ct);

        Assert.False(second.FromCache);
        Assert.Equal(2, _repository.GetCalls);
    }

    [Fact]
    public async Task Get_of_missing_product_is_not_cached()
    {
        var lookup = await _service.GetAsync(Guid.NewGuid(), Ct);

        Assert.Null(lookup.Product);
        Assert.Empty(_cache.Entries);
    }

    [Fact]
    public async Task List_without_paging_returns_first_page_of_default_size()
    {
        _repository.Add();

        var result = await _service.ListAsync(null, null, Ct);

        Assert.Null(result.Errors);
        Assert.Equal((0, ProductService.DefaultPageSize), _repository.LastListRequest);
        Assert.Equal(1, result.Value?.Total);
    }

    [Theory]
    [InlineData(-1, 20, "skip")]
    [InlineData(0, 0, "take")]
    [InlineData(0, ProductService.MaxPageSize + 1, "take")]
    public async Task List_rejects_invalid_paging(int skip, int take, string field)
    {
        var result = await _service.ListAsync(skip, take, Ct);

        Assert.Equal([field], result.Errors?.Keys);
        Assert.Null(_repository.LastListRequest);
    }

    [Fact]
    public async Task Delete_removes_product_and_its_cache_entry()
    {
        var product = _repository.Add();
        await _cache.SetAsync(product);

        Assert.True(await _service.DeleteAsync(product.Id, Ct));
        Assert.Empty(_repository.Products);
        Assert.Equal([product.Id], _cache.Removed);
    }

    [Fact]
    public async Task Delete_of_missing_product_does_not_touch_cache()
    {
        Assert.False(await _service.DeleteAsync(Guid.NewGuid(), Ct));
        Assert.Empty(_cache.Removed);
    }

    private ProductService CreateService(FakeProductCache cache) =>
        new(_repository, cache, new CurrencyCatalog(new FakeCurrencyRepository(), TimeProvider.System));
}
