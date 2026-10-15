namespace Products.Persistence;

// Sekcja "Persistence" konfiguracji - wybór jednego z 4 wariantów (ORM x baza)
public sealed class PersistenceOptions
{
    public const string Section = "Persistence";

    public OrmKind Orm { get; set; } = OrmKind.EfCore;
    public DatabaseEngine Database { get; set; } = DatabaseEngine.Postgres;

    public string Variant => $"{Orm} + {Database}";
}
