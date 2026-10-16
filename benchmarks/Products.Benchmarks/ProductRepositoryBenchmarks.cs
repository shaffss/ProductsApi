using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Persistence;
using Products.Persistence.Dapper;
using Products.Persistence.EfCore;

namespace Products.Benchmarks;

// Repozytoria 4 wariantów na danych z db/*/Publish/benchmark.sql - bez HTTP, cache i Kafki.
// Każda kombinacja parametrów działa w osobnym procesie (domyślne zachowanie BenchmarkDotNet).
[MemoryDiagnoser]
public class ProductRepositoryBenchmarks
{
    // krok względnie pierwszy z liczbą produktów: kolejne wywołania trafiają w różne miejsca indeksu
    // i obejdą wszystkie produkty przed powtórką - ta sama kolejność w każdym wariancie
    private const int Stride = 7919;

    private ServiceProvider _services = null!;
    private IProductRepository _repository = null!;
    private Guid[] _ids = null!;
    private int _next;

    [Params(OrmKind.EfCore, OrmKind.Dapper)]
    public OrmKind Orm { get; set; }

    [Params(DatabaseEngine.Postgres, DatabaseEngine.SqlServer)]
    public DatabaseEngine Database { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var connectionString = BenchmarkData.ConnectionString(Database);
        var services = new ServiceCollection();
        if (Orm == OrmKind.EfCore)
            services.AddEfCorePersistence(Database, connectionString);
        else
            services.AddDapperPersistence(Database, connectionString);
        _services = services.BuildServiceProvider();
        _repository = _services.GetRequiredService<IProductRepository>();

        _ids = [.. Enumerable.Range(1, BenchmarkData.ProductCount).Select(BenchmarkData.ProductId)];
        if (await _repository.GetAsync(_ids[^1], CancellationToken.None) is null)
            throw new InvalidOperationException($"No benchmark data in {Database} - load db/*/Publish/benchmark.sql.");
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await BenchmarkData.CleanupAsync(Database);
        await _services.DisposeAsync();
    }

    [Benchmark]
    public Task<Product?> GetById() => _repository.GetAsync(NextId(), CancellationToken.None);

    [Benchmark]
    public Task<Product> Create() =>
        _repository.CreateAsync(BenchmarkData.CreatedProductName, 10.00m, "PLN", CancellationToken.None);

    // cena spoza zakresu danych (1-999.99) i losowa - każde wywołanie naprawdę zmienia cenę (historia i outbox)
    [Benchmark]
    public Task<PriceUpdate?> ChangePrice() =>
        _repository.ChangePriceAsync(NextId(), Random.Shared.Next(100_000, 10_000_000) / 100m, "PLN", null, CancellationToken.None);

    private Guid NextId() => _ids[_next = (_next + Stride) % _ids.Length];
}
