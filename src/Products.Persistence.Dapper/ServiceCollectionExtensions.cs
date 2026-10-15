using Microsoft.Extensions.DependencyInjection;
using Products.Core.Abstractions;

namespace Products.Persistence.Dapper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDapperPersistence(
        this IServiceCollection services, DatabaseEngine engine, string connectionString)
    {
        services.AddDatabase(engine, connectionString);
        services.AddSingleton<IProductRepository, DapperProductRepository>();
        services.AddSingleton<ICurrencyRepository, DapperCurrencyRepository>();
        return services;
    }
}
