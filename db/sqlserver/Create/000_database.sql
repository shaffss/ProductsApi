-- SQL Server: baza Products
-- Skrypty są idempotentne - można je uruchamiać wielokrotnie, osobno lub scalone (merge_files.cmd).
-- Te same instrukcje wykonuje usługa sqlserver-init w docker-compose.yml - przy zmianie popraw oba miejsca.

IF DB_ID(N'Products') IS NULL
    CREATE DATABASE [Products];
GO

-- READ_COMMITTED_SNAPSHOT (jak MVCC w PostgreSQL): odczyty nie czekają na blokady zapisów i odwrotnie.
-- ROLLBACK IMMEDIATE przerywa inne sesje w tej bazie (zmiana wymaga wyłącznego dostępu).
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'Products' AND is_read_committed_snapshot_on = 0)
    ALTER DATABASE [Products] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
