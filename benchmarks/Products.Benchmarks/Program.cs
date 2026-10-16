using BenchmarkDotNet.Running;
using Products.Benchmarks;
using Products.Persistence;

// dotnet run -c Release --project benchmarks/Products.Benchmarks [-- --filter "*GetById*"]
// dotnet run -c Release --project benchmarks/Products.Benchmarks -- cleanup  (sprzątanie po teście HTTP k6)
if (args is ["cleanup"])
{
    foreach (var engine in Enum.GetValues<DatabaseEngine>())
        await BenchmarkData.CleanupAsync(engine);
    return;
}

BenchmarkRunner.Run<ProductRepositoryBenchmarks>(args: args);
