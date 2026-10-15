using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Products.Persistence.EfCore;

namespace Products.Migrations.Postgres;

// Używana przez dotnet ef (migrations add / database update).
// Domyślnie lokalny kontener z docker-compose.yml; inną bazę wskazuje zmienna ConnectionStrings__Postgres.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ProductsDbContext>()
        .UseNpgsql(
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                ?? "Host=localhost;Port=5432;Database=products;Username=postgres;Password=LocalDev_Passw0rd",
            o => o.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name))
        .Options);
}
