using System.Collections.Concurrent;
using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Products.Persistence;
using Products.Persistence.EfCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Products.IntegrationTests.Fixtures;

// Jeden kontener bazy na cały przebieg testów, w nim osobne bazy (schemat z migracji EF, ze skryptów albo pusta)
public abstract class DatabaseFixture(DatabaseEngine engine, IDatabaseContainer container) : IAsyncLifetime
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _shared = new();

    // CREATE DATABASE + migracje po kolei: równoległe tworzenie wielu baz w SQL Server w kontenerze
    // potrafi przekroczyć limity czasu
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public DatabaseEngine Engine => engine;

    public ValueTask InitializeAsync() => new(container.StartAsync());

    public ValueTask DisposeAsync() => container.DisposeAsync();

    // Baza współdzielona (tworzona raz na klucz) - testy operują na własnych produktach
    public Task<string> GetSharedDatabaseAsync(string key) =>
        _shared.GetOrAdd(key, _ => new(() => CreateDatabaseAsync())).Value;

    // Nowa baza (jak z compose: SQL Server z READ_COMMITTED_SNAPSHOT) ze schematem z migracji EF (jak dotnet ef
    // database update), ze skryptów db/ albo pusta (None) - wtedy schemat zakłada API przy starcie (Database:Schema)
    public async Task<string> CreateDatabaseAsync(SchemaSource schema = SchemaSource.Migrations)
    {
        var name = "test_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = WithDatabase(container.GetConnectionString(), name);

        await _createLock.WaitAsync();
        try
        {
            await CreateEmptyDatabaseAsync(name);
            // baza współdzielona - nie zależy od anulowania jednego testu
            if (schema == SchemaSource.Migrations)
            {
                await using var db = new ProductsDbContext(MigrationOptions(connectionString));
                await db.Database.MigrateAsync(CancellationToken.None);
            }
            else if (schema == SchemaSource.Scripts)
            {
                await SchemaScripts.ApplyAsync(engine, connectionString, CancellationToken.None);
            }
        }
        finally
        {
            _createLock.Release();
        }
        return connectionString;
    }

    private async Task CreateEmptyDatabaseAsync(string name)
    {
        await using DbConnection connection = engine == DatabaseEngine.SqlServer
            ? new SqlConnection(container.GetConnectionString())
            : new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 180;
        command.CommandText = engine == DatabaseEngine.SqlServer
            ? $"CREATE DATABASE [{name}]; ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT ON;"
            : $"CREATE DATABASE \"{name}\"";
        await command.ExecuteNonQueryAsync();
    }

    private DbContextOptions<ProductsDbContext> MigrationOptions(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>();
        if (engine == DatabaseEngine.SqlServer)
            options.UseSqlServer(connectionString, o => o.MigrationsAssembly(DatabaseMigrator.MigrationsAssembly(engine)).CommandTimeout(180));
        else
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly(DatabaseMigrator.MigrationsAssembly(engine)).CommandTimeout(180));
        return options.Options;
    }

    private string WithDatabase(string connectionString, string name) => engine == DatabaseEngine.SqlServer
        ? new SqlConnectionStringBuilder(connectionString) { InitialCatalog = name, ConnectTimeout = 60 }.ConnectionString
        : new NpgsqlConnectionStringBuilder(connectionString) { Database = name, Timeout = 60 }.ConnectionString;
}

// Te same obrazy co w docker-compose.yml. Limit pamięci: domyślnie SQL Server bierze do 80% pamięci
// maszyny Dockera, co przy innych kontenerach (np. z compose) kończy się swapowaniem i timeoutami.
public sealed class SqlServerFixture() : DatabaseFixture(
    DatabaseEngine.SqlServer,
    new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04").WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048").Build());

public sealed class PostgresFixture() : DatabaseFixture(
    DatabaseEngine.Postgres, new PostgreSqlBuilder("postgres:18.6").Build());
