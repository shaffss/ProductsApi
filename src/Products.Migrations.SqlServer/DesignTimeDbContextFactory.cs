using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Products.Persistence.EfCore;

namespace Products.Migrations.SqlServer;

// Używana przez dotnet ef (migrations add / database update).
// Domyślnie lokalny kontener z docker-compose.yml; inną bazę wskazuje zmienna ConnectionStrings__SqlServer.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ProductsDbContext>()
        .UseSqlServer(
            Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
                ?? "Server=localhost,1433;Database=Products;User Id=sa;Password=LocalDev_Passw0rd;TrustServerCertificate=True",
            o => o.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name))
        .Options);
}
