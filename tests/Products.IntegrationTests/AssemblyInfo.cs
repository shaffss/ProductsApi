using Products.IntegrationTests.Fixtures;
using Xunit.Sdk;
using Xunit.v3;

// Kontenery startują raz na cały przebieg testów i są współdzielone przez wszystkie klasy
[assembly: AssemblyFixture(typeof(SqlServerFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]
[assembly: AssemblyFixture(typeof(KafkaFixture))]

// Klasy testów po kolei: równoległe obciążenie wszystkich kontenerów naraz przy małej pamięci Dockera
// (np. 2 GB) kończyło się timeoutami SQL Server. Równoległość wewnątrz testów (np. współbieżne zmiany ceny) zostaje.
[assembly: Parallelization(Mode = ParallelMode.None)]
