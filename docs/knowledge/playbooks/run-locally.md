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
- **Profil `Launcher`** (tylko Windows) pokazuje okno wyboru ORM, bazy, Redisa, Kafki, Aspire Dashboard i schematu przy starcie (brak, migracje EF albo skrypty `db/`); ostatni wybór zapisuje w `%LOCALAPPDATA%\ProductsApi\launcher.json`.
  - **API w kontenerze** (opcja „API”):
    - zamiast uruchamiać API z VS, launcher usuwa kontenery API innych wariantów (zawsze działa jeden), a potem `docker compose --profile <wariant> up -d --build api-<wariant>`: obraz z bieżącego kodu, baza, Redis i Kafka;
    - czeka na `/health/live` i otwiera grid w przeglądarce;
    - przekazuje adres API zamiast argumentów, a `DevLauncher` zamyka proces uruchomiony z VS z komunikatem.

    Ustawienia z okna (Redis, Kafka, schemat) są wtedy wyszarzone, bo kontener ma je z `docker-compose.yml`. VS i tak otwiera kartę `localhost:5032/swagger` (`launchBrowser` profilu), która przy tej opcji nie działa.
  - **Telemetria (Aspire Dashboard):**
    - zaznaczona: launcher uruchamia kontener `aspire-dashboard` (także przy API w kontenerze), a API wysyła do niego telemetrię;
    - niezaznaczona: API dostaje pusty `OTEL_EXPORTER_OTLP_ENDPOINT`, więc nie wysyła telemetrii;
    - przy API w kontenerze opcja decyduje tylko o uruchomieniu dashboardu - kontener wysyła telemetrię zawsze (`docker-compose.yml`).
  - **Status kontenerów** w oknie (PostgreSQL, SQL Server, Redis, Kafka, Aspire Dashboard, z `docker ps`):
    - kropka zielona - działa, pomarańczowa - startuje (healthcheck), czerwona - zatrzymany, szara - brak kontenera albo Docker nie odpowiada;
    - nazwy są linkami: API (grid kontenera wariantu) i Aspire Dashboard (http://localhost:18888) otwierają się w przeglądarce, a PostgreSQL, SQL Server, Redis i Kafka kopiują do schowka adres z `appsettings.Development.json`, np. do SSMS albo DBeavera;
    - pogrubione są kontenery potrzebne wybranemu wariantowi, a podsumowanie mówi, czy wszystkie działają, czy któreś zostaną uruchomione;
    - status jest w osobnej ramce; odświeża się przy otwarciu okna i przyciskiem z ikoną ⟳ w nagłówku ramki, obok daty i godziny ostatniego odświeżenia; zmiana wariantu tylko przelicza potrzebne kontenery.
  - **Kontenery (compose):** przed startem API uruchamia tylko to, czego wariant potrzebuje - bazę (`postgres` albo `sqlserver`), a Redis, Kafkę i Aspire Dashboard, jeśli są zaznaczone. `docker compose up -d --wait` czeka na gotowość.
  - **Baza SQL Server:** zakładana tym samym poleceniem co `sqlserver-init` (odczytanym z `docker compose config`), ale przez `docker compose exec` w działającym kontenerze `sqlserver`. Nie powstaje nowy kontener, więc jest szybciej i działa nawet wtedy, gdy Docker Desktop nie uruchamia nowych kontenerów. `--wait` i tak nie nadaje się do jednorazowego kontenera, bo jego zakończenie traktuje jak błąd.
  - **Błąd Dockera** pokazuje komunikat z wyjściem `docker compose`. Okno zostaje otwarte, a API z VS czeka, więc po np. restarcie Docker Desktop można kliknąć „Uruchom” ponownie.
  - **Po starcie okno zostaje otwarte** jako panel statusu kontenerów (linki, odświeżanie):
    - okno nie jest już na wierzchu i da się je zminimalizować;
    - przy API z VS opcje są zablokowane, bo API działa już z tym wyborem, a okno zamyka się samo razem z procesem API (dostaje jego PID, sprawdza co 2 s);
    - przy API w kontenerze proces z VS kończy pracę, a panel zostaje do zamknięcia. Można w nim zmienić ORM, bazę i dashboard, a „Uruchom” przebudowuje obraz z bieżącego kodu i uruchamia wybrany wariant ponownie (np. po zmianach w kodzie). Przełączyć się na API z VS się nie da, bo nie ma już procesu, który przyjąłby argumenty;
    - zamknięcie okna przed „Uruchom” anuluje start API.
  - **Okno postępu:**
    - bieżący krok (sprawdzanie obrazów, kontenery, baza Products) i stan każdego kontenera, np. „postgres: czeka na gotowość (healthcheck), redis: gotowy”, z licznikiem sekund;
    - podgląd wyjścia `docker compose` na żywo (tworzenie, start, healthcheck, pobieranie obrazów);
    - przycisk „Przerwij” zabija `docker compose` i anuluje start API.

    Wyjście jest czytane linia po linii (`ReadLineAsync` sprawdzane w pętli), bez wątków - zdarzeń .NET z innych wątków PowerShell nie obsłuży.
  - **Limit czasu:**
    - `up` ma 90 s, gdy obrazy są już pobrane (`docker image inspect`) - start trwa wtedy sekundy, więc zawieszony Docker Desktop daje komunikat po półtorej minuty;
    - `up` ma 600 s, gdy obrazy trzeba pobrać;
    - `exec` ma 120 s.

    Szczegóły: [pułapka](../pitfalls/docker-desktop-bind-mount.md).
  - **Status „utknął przy starcie”:** kontener w stanie `created` to objaw zawieszonego Docker Desktop. Status pokazuje go na czerwono, a podsumowanie każe zrestartować Docker Desktop przed startem.
  - **Wynik na stdout:** argumenty dla API (albo adres API w kontenerze) zakończone pustą linią. `DevLauncher` czyta do tej linii i nie czeka na koniec procesu okna. Wyjście `docker compose` nie trafia na stdout.
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
