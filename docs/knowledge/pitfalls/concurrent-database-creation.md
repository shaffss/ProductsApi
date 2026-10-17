---
type: Pitfall
title: Równoległe tworzenie bazy przy pierwszym starcie
description: "Dwie instancje API z migracjami przy starcie na pustym serwerze tworzą bazę naraz - jedna pada z błędem 1801, bo blokada migracji EF działa dopiero w istniejącej bazie."
resource: ../../../docker-compose.yml
tags: [docker, migrations, ef-core]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T22:00:00Z }
---

- **Objaw:** przy pierwszym `docker compose up -d` na pustych wolumenach jeden z kontenerów API SQL Server kończy się wyjątkiem `Database 'Products' already exists` (błąd 1801). Restartuje się i za drugim razem działa.
- **Przyczyna:** `MigrateAsync` najpierw sprawdza, czy baza istnieje, i w razie potrzeby ją tworzy. Blokadę migracji (EF Core 9+) bierze dopiero potem, w tej bazie. Dwie instancje startujące naraz obie widzą brak bazy i obie wykonują `CREATE DATABASE`. W PostgreSQL możliwy jest ten sam wyścig.
- **Rozwiązanie:** baza powstaje przed startem API.
  - PostgreSQL: `POSTGRES_DB` w kontenerze `postgres`, a healthcheck `pg_isready -h localhost` przez TCP. W czasie inicjalizacji serwer słucha tylko na sockecie, więc kontener jest „gotowy” dopiero po założeniu bazy.
  - SQL Server: jednorazowy kontener `sqlserver-init` z instrukcjami jak w [000_database.sql](../../../db/sqlserver/Create/000_database.sql), wpisanymi w polecenie `sqlcmd -Q` - bez montowania pliku ([pułapka](docker-desktop-bind-mount.md)). API czekają na jego zakończenie (`service_completed_successfully`).

  API już tylko migrują - pod blokadą EF, po kolei.
- **Na produkcji** migracje powinien wykonywać jeden osobny krok wdrożenia, a nie każda instancja.

Powiązane: [Migracje EF jako źródło prawdy schematu](../decisions/migrations-source-of-truth.md)
