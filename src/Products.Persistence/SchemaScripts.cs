using System.Data.Common;
using System.Text.RegularExpressions;
using Npgsql;

namespace Products.Persistence;

// Schemat ze skryptów db/<baza>/Create (zasoby assembly, w kolejności nazw) - droga bez EF Core.
// Baza musi istnieć: zakłada ją compose (POSTGRES_DB, sqlserver-init) albo 000_database.sql, który wymaga
// sqlcmd/psql i nie jest tu wykonywany. Skrypty są idempotentne - na gotowej bazie niczego nie zmieniają.
public static partial class SchemaScripts
{
    public static async Task ApplyAsync(DatabaseEngine engine, string connectionString, CancellationToken ct)
    {
        await using DbDataSource dataSource = engine == DatabaseEngine.SqlServer
            ? new SqlServerDataSource(connectionString)
            : NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // jedna instancja naraz, jak blokada migracji EF - równoległe CREATE ... IF NOT EXISTS potrafią kolidować
        await connection.ExecuteAsync(transaction, engine == DatabaseEngine.SqlServer
            ? "EXEC sp_getapplock @Resource = 'Products.Schema', @LockMode = 'Exclusive', @LockOwner = 'Transaction'"
            : "SELECT pg_advisory_xact_lock(hashtext('Products.Schema'))", [], ct);
        foreach (var batch in Batches(engine))
            await connection.ExecuteAsync(transaction, batch, [], ct);

        await transaction.CommitAsync(ct);
    }

    private static IEnumerable<string> Batches(DatabaseEngine engine)
    {
        var assembly = typeof(SchemaScripts).Assembly;
        var prefix = $"db/{engine.ToString().ToLowerInvariant()}/";
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var script = reader.ReadToEnd();

            // SQL Server: GO to separator sqlcmd, a nie T-SQL; USE [Products] pomijane - bazę wskazuje connection string
            foreach (var batch in engine == DatabaseEngine.SqlServer ? GoSeparator().Split(script) : [script])
            {
                if (!string.IsNullOrWhiteSpace(batch) && !UseStatement().IsMatch(batch))
                    yield return batch;
            }
        }
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    [GeneratedRegex(@"^\s*USE\s+\[\w+\];?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex UseStatement();
}
