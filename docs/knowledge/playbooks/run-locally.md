---
type: Playbook
title: Uruchomienie lokalne
description: "Wszystko w Dockerze albo infrastruktura w Dockerze i API z dotnet run / Visual Studio: profile, okno wyboru wariantu, telemetria."
resource: ../../../docker-compose.yml
tags: [setup, docker, development]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Wszystko w Dockerze

Wymaga Docker Desktop z co najmniej 6 GB pamięci - przy 2 GB SQL Server nie mieści się obok reszty kontenerów.

```bash
docker compose up -d            # infrastruktura, 4 warianty API (8081-8084) i Aspire Dashboard (18888)
docker compose up -d --build    # po zmianach w kodzie
```

Kontenery API same stosują migracje przy starcie (`Database__MigrateOnStartup=true`). Bazy zakładane są wcześniej: PostgreSQL przez `POSTGRES_DB`, SQL Server przez jednorazowy kontener `sqlserver-init` - [pułapka](../pitfalls/concurrent-database-creation.md).

# API z dotnet run albo Visual Studio

```bash
docker compose up -d sqlserver postgres redis kafka aspire-dashboard
dotnet tool restore
dotnet ef database update --project src/Products.Migrations.Postgres
dotnet run --project src/Products.Api
```

- **Sekwencja z zadania** (`docker compose up -d`, `dotnet ef database update`, `dotnet run --project src/Products.Api`) działa z jedną różnicą: `dotnet ef` wymaga `--project`, bo migracje są osobne dla każdej bazy. Bez parametru kończy się błędem „No project was found”. Po pełnym `docker compose up -d` migracje wykonały już kontenery API.
- **Warianty SQL Server** wymagają `dotnet ef database update --project src/Products.Migrations.SqlServer`.

- **Profile:** `EfCore-Postgres`, `EfCore-SqlServer`, `Dapper-Postgres`, `Dapper-SqlServer` - wszystkie na http://localhost:5032, VS otwiera Swaggera. Pierwszy na liście jest domyślny dla `dotnet run` bez `--launch-profile`.
- **Profil `Launcher`** (tylko Windows) pokazuje okno wyboru ORM, bazy, Redisa, Kafki i migracji przy starcie; ostatni wybór zapisuje w `%LOCALAPPDATA%\ProductsApi\launcher.json`.
  - Musi być ostatni na liście, inaczej `dotnet run` bez profilu otwierałby okno (a na Linuksie kończył się błędem).
  - Okno to skrypt `DevLauncher/launcher.ps1` (WinForms) uruchamiany jako osobny proces. Wybór trafia do API jako argumenty `--Klucz=wartość`, więc debugger VS zostaje podpięty do API.
  - Skrypt musi mieć BOM UTF-8 (Windows PowerShell 5.1 czyta pliki bez BOM jako ANSI i psuje polskie znaki) i nie trafia do publikacji ani obrazu Dockera.
  - Profile zamiast menu w konsoli przy starcie: menu zawiesiłoby się w kontenerze i w CI.
- **Bez profilu:** `dotnet run --project src/Products.Api -- --Persistence:Orm=Dapper --Persistence:Database=SqlServer`.
- **Telemetria:** w Development API wysyła ją do Aspire Dashboard (`localhost:4317`); pusty `OTEL_EXPORTER_OTLP_ENDPOINT` ją wyłącza.

# Konfiguracja

- Klucze z komentarzami: [appsettings.json](../../../src/Products.Api/appsettings.json).
- Adresy lokalnych kontenerów: [appsettings.Development.json](../../../src/Products.Api/appsettings.Development.json).
- Tabela kluczy: [README](../../../README.md).

Hasło `LocalDev_Passw0rd` służy wyłącznie lokalnym kontenerom.

Powiązane: [Cztery warianty dostępu do danych](../decisions/four-variants.md), [Testy, format i CI](run-tests.md)
