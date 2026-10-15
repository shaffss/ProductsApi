using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Products.Core.Abstractions;

namespace Products.Persistence.EfCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEfCorePersistence(
        this IServiceCollection services, DatabaseEngine engine, string connectionString)
    {
        services.AddDatabase(engine, connectionString);

        // pula kontekstów: repozytoria są singletonami i biorą kontekst z puli na czas jednej operacji.
        // Ponawianie przy błędach przejściowych - ta sama polityka co w Dapperze (DbRetry).
        services.AddPooledDbContextFactory<ProductsDbContext>((provider, options) =>
        {
            var migrationsAssembly = DatabaseMigrator.MigrationsAssembly(engine);
            if (engine == DatabaseEngine.SqlServer)
                options.UseSqlServer(connectionString, o => o
                    .MigrationsAssembly(migrationsAssembly)
                    .EnableRetryOnFailure(DbRetry.MaxRetries, DbRetry.MaxDelay, errorNumbersToAdd: null));
            else // to samo NpgsqlDataSource co health check - jedna pula połączeń
                options.UseNpgsql(provider.GetRequiredService<DbDataSource>(), o => o
                    .MigrationsAssembly(migrationsAssembly)
                    .EnableRetryOnFailure(DbRetry.MaxRetries, DbRetry.MaxDelay, errorCodesToAdd: null));
        });

        services.AddSingleton<IProductRepository, EfProductRepository>();
        services.AddSingleton<ICurrencyRepository, EfCurrencyRepository>();
        return services;
    }
}
