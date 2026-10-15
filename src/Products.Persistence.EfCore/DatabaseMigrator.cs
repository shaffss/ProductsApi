using Microsoft.EntityFrameworkCore;

namespace Products.Persistence.EfCore;

// Schemat bazy z migracji EF - wspólny dla obu ORM (Dapper korzysta z tych samych tabel)
public static class DatabaseMigrator
{
    public static string MigrationsAssembly(DatabaseEngine engine) =>
        engine == DatabaseEngine.SqlServer ? "Products.Migrations.SqlServer" : "Products.Migrations.Postgres";

    // Jak dotnet ef database update: tworzy bazę, jeśli nie istnieje, i stosuje brakujące migracje.
    // EF blokuje bazę na czas migracji, więc kilka instancji startujących naraz jest bezpieczne.
    public static async Task MigrateAsync(DatabaseEngine engine, string connectionString, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>();
        if (engine == DatabaseEngine.SqlServer)
            options.UseSqlServer(connectionString, o => o.MigrationsAssembly(MigrationsAssembly(engine)));
        else
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly(MigrationsAssembly(engine)));

        await using var db = new ProductsDbContext(options.Options);
        await db.Database.MigrateAsync(ct);
    }
}
