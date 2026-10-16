using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Products.Persistence;

namespace Products.Benchmarks;

// Dane z db/*/Publish/benchmark.sql i sprzątanie po pomiarach (także po teście HTTP k6)
public static class BenchmarkData
{
    public const int ProductCount = 100_000;

    // nazwa produktów tworzonych w pomiarach - inna niż "Bench %" z generatora, więc go nie zaburza
    public const string CreatedProductName = "Benchmark create";

    // Id jak w generatorze: b0000000-0000-0000-0000-{n w hex}, n = 1..ProductCount
    public static Guid ProductId(int n) => Guid.Parse($"b0000000-0000-0000-0000-{n:x12}");

    // Bazy z docker-compose.yml (jak w appsettings.Development.json API); inne przez zmienne jak w API,
    // np. ConnectionStrings__Postgres
    public static string ConnectionString(DatabaseEngine engine) =>
        Environment.GetEnvironmentVariable($"ConnectionStrings__{engine}") ?? engine switch
        {
            DatabaseEngine.SqlServer => "Server=localhost,1433;Database=Products;User Id=sa;Password=LocalDev_Passw0rd;TrustServerCertificate=True",
            _ => "Host=localhost;Port=5432;Database=products;Username=postgres;Password=LocalDev_Passw0rd",
        };

    // Usuwa utworzone produkty i niewysłane zdarzenia zmian cen produktów benchmarkowych - po uruchomieniu API
    // z Kafką nie trafią do niej tysiące zdarzeń z pomiarów. Historia cen zostaje (nie wpływa na pomiary).
    public static async Task CleanupAsync(DatabaseEngine engine)
    {
        await using var services = new ServiceCollection().AddDatabase(engine, ConnectionString(engine)).BuildServiceProvider();
        await using var command = services.GetRequiredService<DbDataSource>().CreateCommand(engine == DatabaseEngine.SqlServer
            ? $"""
              DELETE FROM dbo.OutboxMessages WHERE ProcessedAtUtc IS NULL AND MessageKey LIKE 'b0000000-0000-0000-0000-%';
              DELETE FROM dbo.Products WHERE Name = '{CreatedProductName}';
              """
            : $"""
              DELETE FROM "OutboxMessages" WHERE "ProcessedAtUtc" IS NULL AND "MessageKey" LIKE 'b0000000-0000-0000-0000-%';
              DELETE FROM "Products" WHERE "Name" = '{CreatedProductName}';
              """);
        await command.ExecuteNonQueryAsync();
    }
}
