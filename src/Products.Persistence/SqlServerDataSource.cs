using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Products.Persistence;

// SqlClient nie ma własnego DbDataSource (Npgsql ma NpgsqlDataSource) - wspólna abstrakcja dla obu baz.
// Pula połączeń działa w SqlClient na poziomie connection stringa.
internal sealed class SqlServerDataSource(string connectionString) : DbDataSource
{
    public override string ConnectionString => connectionString;

    protected override DbConnection CreateDbConnection() => new SqlConnection(connectionString);
}
