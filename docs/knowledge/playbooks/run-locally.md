---
type: Playbook
title: Uruchomienie lokalne
description: "Jeden wariant w Dockerze (profile Compose) albo infrastruktura w Dockerze i API z dotnet run / Visual Studio: profile, okno wyboru wariantu, telemetria."
resource: ../../../docker-compose.yml
tags: [setup, docker, development]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Wariant w Dockerze

Wymaga Docker Desktop z co najmniej 6 GB pamięci - przy 2 GB SQL Server nie mieści się obok reszty kontenerów.

```bash
docker compose --profile efcore-postgres up -d            # jeden wariant: jego baza, Redis, Kafka i API (8081)
docker compose --profile efcore-postgres up -d --build    # po zmianach w kodzie
docker compose --profile "*" down                         # zatrzymanie i usunięcie wszystkich kontenerów projektu
```

- **Profile wariantów:** `efcore-postgres` (8081), `efcore-sqlserver` (8082), `dapper-postgres` (8083), `dapper-sqlserver` (8084). Każdy uruchamia tylko minimum dla wariantu, bo zawsze działa jeden.
- **Aspire Dashboard** (18888) dochodzi z `--profile observability`. Bez niego API pomija telemetrię.
- **Sprzątanie:**
  - `down` usuwa kontenery i sieć, a dane zostają w wolumenach; `down -v` usuwa także dane.
  - Samo `docker compose down` pomija kontenery z profili, których polecenie nie wskazuje, stąd `--profile "*"`.
  - Kontenery nie mają polityki restartu, więc po restarcie Dockera nic nie wstaje samo.

Kontenery API same zakładają schemat przy starcie (`Database__Schema`): warianty EF z migracji, warianty Dappera ze skryptów `db/` - ten sam wynik ([decyzja](../decisions/migrations-source-of-truth.md)). Bazy zakładane są wcześniej: PostgreSQL przez `POSTGRES_DB`, SQL Server przez jednorazowy kontener `sqlserver-init` - [pułapka](../pitfalls/concurrent-database-creation.md).

# API z dotnet run albo Visual Studio

```bash
docker compose up -d                                      # PostgreSQL, Redis, Kafka (profil z .env)
dotnet tool restore
dotnet ef database update --project src/Products.Migrations.Postgres
dotnet run --project src/Products.Api
```

- **Sekwencja z zadania** (`docker compose up -d`, `dotnet ef database update`, `dotnet run --project src/Products.Api`) działa z jedną różnicą: `dotnet ef` wymaga `--project`, bo migracje są osobne dla każdej bazy. Bez parametru kończy się błędem „No project was found”.
- **Warianty SQL Server:** `docker compose --profile sqlserver up -d` i `dotnet ef database update --project src/Products.Migrations.SqlServer`.

- **Profile:** `EfCore-Postgres`, `EfCore-SqlServer`, `Dapper-Postgres`, `Dapper-SqlServer` - wszystkie na http://localhost:5032, VS otwiera Swaggera. Pierwszy na liście jest domyślny dla `dotnet run` bez `--launch-profile`.
- **Profil `Launcher`** (tylko Windows) pokazuje okno wyboru ORM, bazy, Redisa, Kafki i schematu przy starcie (brak, migracje EF albo skrypty `db/`); ostatni wybór zapisuje w `%LOCALAPPDATA%\ProductsApi\launcher.json`.
  - **Kontenery (compose):** przed startem API uruchamia tylko to, czego wariant potrzebuje - bazę (`postgres` albo `sqlserver`), a Redis i Kafkę, jeśli są zaznaczone. `docker compose up -d --wait` czeka na gotowość; jednorazowy `sqlserver-init` idzie osobno (`run --rm`), bo `--wait` traktuje zakończony kontener jak błąd. Błąd Dockera pokazuje okno z wyjściem `docker compose` i anuluje start.
  - Wyjście `docker compose` nie trafia na stdout skryptu, bo tam są argumenty dla API.
  - Musi być ostatni na liście, inaczej `dotnet run` bez profilu otwierałby okno (a na Linuksie kończył się błędem).
  - Okno to skrypt `DevLauncher/launcher.ps1` (WinForms) uruchamiany jako osobny proces. Wybór trafia do API jako argumenty `--Klucz=wartość`, więc debugger VS zostaje podpięty do API.
  - Skrypt musi mieć BOM UTF-8 (Windows PowerShell 5.1 czyta pliki bez BOM jako ANSI i psuje polskie znaki) i nie trafia do publikacji ani obrazu Dockera.
  - Profile zamiast menu w konsoli przy starcie: menu zawiesiłoby się w kontenerze i w CI.
- **Bez profilu:** `dotnet run --project src/Products.Api -- --Persistence:Orm=Dapper --Persistence:Database=SqlServer`.
- **Telemetria:** w Development API wysyła ją do Aspire Dashboard (`localhost:4317`), np. z `docker compose --profile postgres --profile observability up -d`. Pusty `OTEL_EXPORTER_OTLP_ENDPOINT` ją wyłącza.

# Konfiguracja

- Klucze z komentarzami: [appsettings.json](../../../src/Products.Api/appsettings.json).
- Adresy lokalnych kontenerów: [appsettings.Development.json](../../../src/Products.Api/appsettings.Development.json).
- Tabela kluczy: [README](../../../README.md).

Hasło `LocalDev_Passw0rd` służy wyłącznie lokalnym kontenerom.

Powiązane: [Cztery warianty dostępu do danych](../decisions/four-variants.md), [Testy, format i CI](run-tests.md)
