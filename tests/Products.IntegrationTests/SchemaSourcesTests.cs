using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Products.IntegrationTests.Fixtures;
using Products.Persistence;
using Products.Persistence.EfCore;

namespace Products.IntegrationTests;

// Dwie drogi do schematu (Database:Schema) - migracje EF i skrypty db/<baza>/Create - dają ten sam schemat i tę samą
// historię migracji, więc warianty mogą na zmianę używać jednej bazy. Porównanie zapytaniami z bazy wiedzy
// (docs/knowledge/references/schema-dump-*.sql), tymi samymi co w instrukcji zmiany schematu.
public abstract class SchemaSourcesTests(DatabaseFixture database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scripts_create_the_same_schema_and_migration_history_as_migrations()
    {
        var fromMigrations = await DumpSchemaAsync(await database.CreateDatabaseAsync(SchemaSource.Migrations));
        var fromScripts = await DumpSchemaAsync(await database.CreateDatabaseAsync(SchemaSource.Scripts));

        Assert.Equal(fromMigrations, fromScripts);
        Assert.Contains(fromScripts, line => line.StartsWith("COL Products.", StringComparison.Ordinal));
        Assert.Contains(fromScripts, line => line.StartsWith("MIGRATION ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sources_can_be_switched_on_one_database()
    {
        var fromScripts = await database.CreateDatabaseAsync(SchemaSource.Scripts);
        var fromMigrations = await database.CreateDatabaseAsync(SchemaSource.Migrations);
        var expected = await DumpSchemaAsync(fromMigrations);

        // EF uznaje bazę ze skryptów za zmigrowaną (inaczej próbowałby tworzyć istniejące tabele), skrypty są
        // idempotentne i niczego nie zmieniają w bazie z migracji
        await DatabaseMigrator.MigrateAsync(database.Engine, fromScripts, Ct);
        await SchemaScripts.ApplyAsync(database.Engine, fromScripts, Ct);
        await SchemaScripts.ApplyAsync(database.Engine, fromMigrations, Ct);

        Assert.Equal(expected, await DumpSchemaAsync(fromScripts));
        Assert.Equal(expected, await DumpSchemaAsync(fromMigrations));
    }

    private async Task<List<string>> DumpSchemaAsync(string connectionString)
    {
        var engine = database.Engine;
        var dump = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "SchemaDumps", $"schema-dump-{engine.ToString().ToLowerInvariant()}.sql"), Ct);
        var history = engine == DatabaseEngine.SqlServer
            ? "SELECT CONCAT('MIGRATION ', MigrationId) FROM dbo.__EFMigrationsHistory ORDER BY MigrationId"
            : """SELECT 'MIGRATION ' || "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" """;

        await using var services = new ServiceCollection().AddDatabase(engine, connectionString).BuildServiceProvider();
        var dataSource = services.GetRequiredService<DbDataSource>();
        var lines = new List<string>();
        foreach (var sql in new[] { dump, history })
        {
            await using var command = dataSource.CreateCommand(sql);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct))
                lines.Add(reader.GetString(0));
        }
        return lines;
    }
}

public sealed class SqlServerSchemaSourcesTests(SqlServerFixture database) : SchemaSourcesTests(database);

public sealed class PostgresSchemaSourcesTests(PostgresFixture database) : SchemaSourcesTests(database);
