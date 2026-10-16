namespace Products.Persistence;

// Database:Schema - skąd API bierze schemat bazy przy starcie. Obie drogi dają ten sam schemat i tę samą
// historię migracji EF (test SchemaSourcesTests), więc warianty mogą na zmianę używać jednej bazy.
public enum SchemaSource
{
    None, // schemat już jest: dotnet ef database update albo db/<baza>/Publish/create.sql
    Migrations, // migracje EF Core (DatabaseMigrator)
    Scripts, // skrypty db/<baza>/Create, bez EF (SchemaScripts)
}
