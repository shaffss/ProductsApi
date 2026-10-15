using Products.Core;
using Products.Infrastructure;
using Products.Infrastructure.Caching;
using Products.Infrastructure.Messaging;
using Products.Persistence;
using Products.Persistence.Dapper;
using Products.Persistence.EfCore;

namespace Products.Api;

public static class ServiceCollectionExtensions
{
    // Wariant (ORM x baza) z sekcji "Persistence"; connection string o nazwie bazy (ConnectionStrings:SqlServer / :Postgres).
    // Retencja danych technicznych z sekcji "Retention".
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(PersistenceOptions.Section).Get<PersistenceOptions>() ?? new();
        var connectionString = configuration.GetConnectionString(options.Database.ToString())
            ?? throw new InvalidOperationException($"Brak ConnectionStrings:{options.Database} w konfiguracji.");

        if (options.Orm == OrmKind.Dapper)
            services.AddDapperPersistence(options.Database, connectionString);
        else
            services.AddEfCorePersistence(options.Database, connectionString);

        services.AddDatabaseRetention(configuration.GetSection(RetentionOptions.Section).Get<RetentionOptions>() ?? new());
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CurrencyCatalog>();
        return services;
    }

    // Redis z ConnectionStrings:Redis, ustawienia z sekcji "Cache"
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration) =>
        services.AddProductCache(
            configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new(),
            configuration.GetConnectionString("Redis"));

    // Wysyłka outboxa i konsument zdarzeń, ustawienia z sekcji "Kafka"
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration) =>
        services.AddKafkaMessaging(configuration.GetSection(KafkaOptions.Section).Get<KafkaOptions>() ?? new());
}
