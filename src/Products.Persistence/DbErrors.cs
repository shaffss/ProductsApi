using Microsoft.Data.SqlClient;
using Npgsql;

namespace Products.Persistence;

// Rozpoznawanie błędów bazy - wspólne dla obu ORM i obu baz
public static class DbErrors
{
    // Naruszenie klucza głównego lub unikalnego (np. klucz idempotencji zapisany już przez inne żądanie)
    public static bool IsUniqueViolation(Exception? e) =>
        e is SqlException { Number: 2627 or 2601 } or PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    // Błąd przejściowy - ponowienie ma sens (zerwane połączenie, deadlock, chwilowa niedostępność bazy).
    // Npgsql klasyfikuje sam; SqlClient nie (IsTransient zawsze false) - numery błędów jak w EF Core.
    // Bez timeoutu (-2): polecenie mogło się wykonać, a ponowienie dokłada obciążenia przeciążonej bazie.
    public static bool IsTransient(Exception e) => e switch
    {
        SqlException sql => sql.Errors.Cast<SqlError>().Any(error => error.Number is
            1205 // deadlock
            or 20 or 64 or 121 or 233 or 10053 or 10054 or 10060 // połączenie
            or 4060 or 4221 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 // baza chwilowo niedostępna
            or 10928 or 10929 or 10936), // limity zasobów (Azure SQL)
        NpgsqlException npgsql => npgsql.IsTransient,
        _ => false,
    };
}
