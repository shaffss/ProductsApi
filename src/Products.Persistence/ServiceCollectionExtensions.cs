using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Products.Core.Abstractions;

namespace Products.Persistence;

public static class ServiceCollectionExtensions
{
    // Wspólne dla obu ORM: dialekt, źródło połączeń, health check bazy oraz outbox i idempotentny konsument
    public static IServiceCollection AddDatabase(this IServiceCollection services, DatabaseEngine engine, string connectionString)
    {
        // fabryka zamiast instancji, żeby kontener zwolnił źródło danych przy zamknięciu aplikacji
        services.AddSingleton<DbDataSource>(_ => engine == DatabaseEngine.SqlServer
            ? new SqlServerDataSource(connectionString)
            : NpgsqlDataSource.Create(connectionString));

        services.AddSingleton(SqlDialect.For(engine));
        services.AddSingleton<IOutboxStore, OutboxStore>();
        services.AddSingleton<IProcessedEventStore, ProcessedEventStore>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    // Okresowe usuwanie starych danych technicznych (sekcja "Retention")
    public static IServiceCollection AddDatabaseRetention(this IServiceCollection services, RetentionOptions options)
    {
        if (!options.Enabled)
            return services;

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<DatabaseRetention>();
        return services;
    }
}
