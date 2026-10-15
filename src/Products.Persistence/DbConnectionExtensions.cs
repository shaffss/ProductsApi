using System.Data.Common;

namespace Products.Persistence;

// Minimalny ADO.NET dla kodu infrastruktury (outbox, idempotentny konsument) - wspólny dla obu wariantów ORM
internal static class DbConnectionExtensions
{
    public static async Task<int> ExecuteAsync(
        this DbConnection connection, DbTransaction transaction, string sql, DbParameter[] parameters, CancellationToken ct)
    {
        await using var command = connection.CreateCommand(transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task<T> ScalarAsync<T>(
        this DbConnection connection, DbTransaction transaction, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand(transaction, sql, []);
        return (T)(await command.ExecuteScalarAsync(ct))!;
    }

    public static DbCommand CreateCommand(
        this DbConnection connection, DbTransaction transaction, string sql, DbParameter[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        return command;
    }
}
