using System.Data.Common;
using BenchmarkDotNet.Running;
using Products.Benchmarks;
using Products.Persistence;

// dotnet run -c Release --project benchmarks/Products.Benchmarks [-- --filter "*GetById*"]
// dotnet run -c Release --project benchmarks/Products.Benchmarks -- cleanup  (sprzątanie po teście HTTP k6)
if (args is ["cleanup"])
{
    // przy k6 działa tylko baza jednego wariantu - niedostępna jest pomijana
    foreach (var engine in Enum.GetValues<DatabaseEngine>())
    {
        try
        {
            await BenchmarkData.CleanupAsync(engine);
            Console.WriteLine($"{engine}: cleaned");
        }
        catch (DbException e)
        {
            Console.WriteLine($"{engine}: skipped - {e.Message}");
        }
    }
    return;
}

BenchmarkRunner.Run<ProductRepositoryBenchmarks>(args: args);
