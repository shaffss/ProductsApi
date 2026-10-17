# Products API

API w .NET 10 do zarządzania produktami: tworzenie, odczyt i zmiana ceny.

- **Baza danych** (PostgreSQL albo SQL Server) jest źródłem prawdy.
- **Redis** działa jako cache odczytu produktu (cache-aside).
- **Kafka** dostaje zdarzenie `ProductPriceChanged` po każdej zmianie ceny, przez Transactional Outbox. Konsument w tej samej aplikacji zapisuje zmianę w logu.

Dostęp do danych jest zaimplementowany w **4 wariantach**: EF Core albo Dapper, każdy dla PostgreSQL albo SQL Server. Wariant wybiera się w konfiguracji, a pozostała logika (walidacja, cache, Kafka) jest wspólna. Dzięki temu warianty da się porównać w benchmarkach.

## Szybki start

Wymagania:
- Docker Desktop z co najmniej 6 GB pamięci (Settings → Resources). Przy 2 GB SQL Server nie mieści się obok reszty kontenerów.
- .NET 10 SDK, tylko do uruchamiania lokalnego i testów.

Uruchamia się jeden wariant naraz, z minimum kontenerów: jego baza, Redis, Kafka i API. Wariant wybiera profil Compose:

```bash
docker compose --profile efcore-postgres up -d
```

| Profil | API | Swagger | Baza |
|---|---|---|---|
| `efcore-postgres` | http://localhost:8081 | http://localhost:8081/swagger | PostgreSQL |
| `efcore-sqlserver` | http://localhost:8082 | http://localhost:8082/swagger | SQL Server |
| `dapper-postgres` | http://localhost:8083 | http://localhost:8083/swagger | PostgreSQL |
| `dapper-sqlserver` | http://localhost:8084 | http://localhost:8084/swagger | SQL Server |

- **Aspire Dashboard** (ślady, metryki i logi, http://localhost:18888) jest opcjonalny: dodaj `--profile observability`. Bez niego API pomija wysyłkę telemetrii.
- **Bez profilu** `docker compose up -d` uruchamia samą infrastrukturę domyślnego wariantu (PostgreSQL, Redis, Kafka) pod `dotnet run`, zgodnie z sekwencją z zadania. Domyślny profil jest w pliku `.env`.
- **Zatrzymanie:** `docker compose --profile "*" down` usuwa wszystkie kontenery projektu, a dane zostają w wolumenach. Samo `docker compose down` pomija kontenery z profili, których polecenie nie wskazuje. Kontenery nie mają polityki restartu, więc po restarcie Dockera nic nie wstaje samo.

> **Schemat zakładany automatycznie.** Kontener API zakłada schemat swojej bazy przy starcie (`Database__Schema`), więc `dotnet ef database update` nie jest potrzebne. Warianty EF Core stosują migracje EF, a warianty Dappera skrypty z `db/<baza>/Create`, bez EF. Wynik jest ten sam (sprawdza to test), więc warianty mogą na zmianę używać jednej bazy. API startuje dopiero, gdy baza już istnieje. Bazę PostgreSQL zakłada kontener `postgres` (`POSTGRES_DB`), a bazę SQL Server jednorazowy kontener `sqlserver-init` (te same instrukcje co `db/sqlserver/Create/000_database.sql`, wpisane w polecenie `sqlcmd`, bez montowania pliku). Kilka wariantów na jednej bazie (kilka `--profile`) może więc startować jednocześnie, bo zakładanie schematu odbywa się pod blokadą w bazie.

**Frontend:** pod adresem API (np. http://localhost:8081/, lokalnie http://localhost:5032/) jest grid z produktami: stronicowanie z numerami stron i wyborem rozmiaru strony, dodawanie (z `Idempotency-Key`) oraz zaznaczenie wiersza i przyciski obok dodawania: edycja ceny (ołówek; `If-Match` z obsługą konfliktu wersji) i usuwanie (kosz), a w nagłówku przełącznik trybu jasnego i ciemnego (domyślnie systemowy, wybór zapamiętany). To statyczna strona (HTML, CSS i JS bez frameworka i bez budowania) w `src/Products.Api/wwwroot`, serwowana przez API z tego samego adresu (bez CORS).

Działający wariant pokazuje `GET /health` w polu `variant`. Kolekcja Postmana jest opisana w sekcji [Weryfikacja](#weryfikacja).

## Uruchomienie lokalne (dotnet run / Visual Studio)

```bash
docker compose up -d
dotnet tool restore
dotnet ef database update --project src/Products.Migrations.Postgres
dotnet run --project src/Products.Api
```

- **Sekwencja z zadania** (`docker compose up -d`, `dotnet ef database update`, `dotnet run --project src/Products.Api`) działa z jedną różnicą: `dotnet ef` wymaga `--project`, bo migracje są osobne dla każdej bazy. Bez parametru kończy się błędem „No project was found”.
- **Infrastruktura:** `docker compose up -d` uruchamia PostgreSQL, Redis i Kafkę. Dla wariantów SQL Server użyj `docker compose --profile sqlserver up -d`. Z telemetrią dodaj `--profile observability`, np. `docker compose --profile postgres --profile observability up -d`.
- **Migracje:** `dotnet ef database update` tworzy bazę (`products` w PostgreSQL, `Products` w SQL Server) i schemat. Dla wariantów SQL Server: `dotnet ef database update --project src/Products.Migrations.SqlServer`. Connection string do kontenera z compose jest domyślny; inną bazę wskazuje zmienna `ConnectionStrings__Postgres` / `ConnectionStrings__SqlServer`.
- **Domyślny wariant** `dotnet run` (bez profilu) to EF Core + PostgreSQL.
- **Profile uruchomieniowe** (`launchSettings.json`): `EfCore-Postgres`, `EfCore-SqlServer`, `Dapper-Postgres`, `Dapper-SqlServer`. Wszystkie działają na http://localhost:5032. W Visual Studio wybierasz profil z listy przy przycisku Start (projekt startowy: `Products.Api`), a VS otwiera Swaggera.
- **Profil `Launcher`** (tylko Windows) przed startem API pokazuje okno wyboru: ORM, baza, Redis, Kafka, Aspire Dashboard i schemat przy starcie (brak, migracje EF albo skrypty `db/`), ze statusem kontenerów (kropka zielona/czerwona, potrzebne dla wariantu pogrubione) i opcją uruchomienia API w kontenerze wariantu zamiast z VS (bez debuggera; otwiera grid). Opcja „Kontenery (compose)” najpierw uruchamia w Dockerze bazę wybranego wariantu oraz Redis, Kafkę i Aspire Dashboard, jeśli są zaznaczone, i czeka na ich gotowość. Wtedy osobne `docker compose up -d` nie jest potrzebne. Ostatni wybór jest zapamiętany, a debugger VS zostaje podpięty do API. To samo z linii poleceń: `dotnet run --project src/Products.Api -- --launcher`.
- **Wariant bez profilu:**
  ```bash
  dotnet run --project src/Products.Api -- --Persistence:Orm=Dapper --Persistence:Database=SqlServer
  ```
- **Telemetria:** w Development API wysyła ślady, metryki i logi do Aspire Dashboard z compose (profil `observability`, `localhost:4317`, podgląd http://localhost:18888); bez dashboardu telemetria jest pomijana.

### Konfiguracja

| Klucz | Domyślnie | Opis |
|---|---|---|
| `Persistence:Orm` | `EfCore` | `EfCore` albo `Dapper` |
| `Persistence:Database` | `Postgres` | `Postgres` albo `SqlServer`; connection string z `ConnectionStrings:<Database>` |
| `Database:Schema` | `None` | schemat przy starcie: `Migrations` (migracje EF), `Scripts` (skrypty `db/<baza>/Create`, bez EF) albo `None`; w compose EF → `Migrations`, Dapper → `Scripts` |
| `Cache:Enabled` / `Cache:ProductTtl` | `true` / `00:05:00` | Redis (`ConnectionStrings:Redis`); wyłączenie np. w benchmarkach bazy |
| `Kafka:Enabled` | `true` | wysyłka outboxa i konsument; `false` = zdarzenia zostają w outboxie |
| `Kafka:BootstrapServers` | — | lokalnie `localhost:9094`, w compose `kafka:9092` |
| `Kafka:ConsumerGroup` | `products-api` | grupa konsumentów (w compose osobna dla każdego wariantu) |
| `Kafka:OutboxMaxAttempts` | `10` | błędy samej wiadomości (np. za duża); po limicie wiadomość zostaje w outboxie jako dead-letter |
| `Kafka:ConsumerMaxAttempts` | `5` | próby obsługi zdarzenia (odstępy 1 s, 2 s, 4 s, ...); potem zdarzenie jest pomijane z logiem `Error` |
| `Retention:*` | co `01:00:00` | usuwanie wysłanych wiadomości outboxa i obsłużonych zdarzeń (po 7 dniach) oraz kluczy idempotencji (po 1 dniu) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | — | adres OTLP (OpenTelemetry); bez niego telemetria jest wyłączona i nic nie kosztuje |
| `OTEL_SERVICE_NAME` | `products-api` | nazwa usługi w telemetrii (w compose nazwa wariantu) |

Hasła w `docker-compose.yml` i `appsettings.Development.json` (`LocalDev_Passw0rd`) służą wyłącznie do lokalnych kontenerów deweloperskich. To nie są sekrety produkcyjne.

## Endpointy

| Metoda | Endpoint | Odpowiedzi |
|---|---|---|
| `GET` | `/api/products?skip=0&take=20` | `200` + strona od najnowszych (`items`, `total`, `skip`, `take`); `400` (`skip` < 0, `take` poza 1–100) |
| `POST` | `/api/products` | `201` + `Location` + `ETag` + produkt; `400` (walidacja); `422` (klucz idempotencji użyty z inną treścią) |
| `GET` | `/api/products/{id}` | `200` + produkt + `ETag` + `X-Cache: HIT/MISS`; `400` (id nie jest GUID); `404` |
| `PUT` | `/api/products/{id}/price` | `200` + produkt po zmianie + `ETag`; `400` (walidacja, id, `If-Match`); `404`; `412` (nieaktualny `If-Match`) |
| `DELETE` | `/api/products/{id}` | `204`; `400` (id nie jest GUID); `404` |
| `GET` | `/health`, `/health/ready` | gotowość: `200` Healthy/Degraded, `503` gdy baza niedostępna |
| `GET` | `/health/live` | żywotność: `200`, bez sprawdzania zależności |

```http
POST /api/products
Idempotency-Key: "8e03978e-40d5-43e8-bc93-6894a57f9324"
{ "name": "Czujnik temperatury", "price": 149.99, "currency": "PLN" }

201 Created
Location: /api/products/01a13e38-2345-775e-bbe2-8a74877a9eb0
ETag: "1"
{ "id": "01a13e38-...", "name": "Czujnik temperatury", "price": 149.99, "currency": "PLN", "updatedAtUtc": "2026-09-17T08:00:00Z" }

PUT /api/products/{id}/price
If-Match: "1"
{ "price": 129.99, "currency": "PLN" }
```

- **Walidacja:**
  - nazwa jest wymagana (spacje są obcinane, maksymalnie 200 znaków);
  - cena musi być większa od 0, mieć najwyżej 2 miejsca po przecinku i mieścić się w zakresie `decimal(18,2)`;
  - waluta musi być aktywna w słowniku `Currencies` (PLN, EUR, USD). Wielkość liter nie ma znaczenia, a kod jest zapisywany jako `PLN`.
- **Błędy** zwracane są w formacie ProblemDetails (RFC 9457):
  - `400` zawiera listę błędów dla każdego pola;
  - niepoprawny JSON zwraca `400` ze wskazaniem pola (`$.price`).
- **PUT z tą samą ceną i walutą** niczego nie zapisuje (brak historii i zdarzenia) i zwraca `200` z produktem oraz polem `message`.
- **`Idempotency-Key` (opcjonalny, POST):** ponowione żądanie z tym samym kluczem (np. po utracie odpowiedzi) dostaje produkt utworzony za pierwszym razem, z nagłówkiem `Idempotent-Replayed: true`, zamiast tworzyć duplikat. Ten sam klucz z inną treścią to `422`. Treść jest porównywana po normalizacji (`10` i `10.00` to to samo). Klucz: 1–100 widocznych znaków ASCII, w cudzysłowie (jak w specyfikacji IETF) albo bez, ważny 24 h. Równoległe żądania z tym samym kluczem tworzą jeden produkt (klucz główny w tej samej transakcji co produkt).
- **Lista** (`GET /api/products`): od najnowszych (`CreatedAtUtc`, potem `Id`), stronicowanie `skip`/`take` (domyślnie 0 i 20, `take` do 100), `total` to liczba wszystkich produktów. Odczyt z bazy, bez cache. Indeks `(CreatedAtUtc, Id)` jest czytany wstecz; dalekie strony są wolniejsze (baza pomija `skip` wierszy), ale na 100 tys. produktów to kilkadziesiąt milisekund.
- **Usuwanie** (`DELETE`): produkt z historią cen i kluczami idempotencji w jednej transakcji, potem usunięcie wpisu z Redisa. Zdarzenia nie ma; niewysłane zdarzenia zmian ceny tego produktu zostają w outboxie i zostaną wysłane.
- **`ETag` / `If-Match` (opcjonalny, PUT):** `ETag` to wersja produktu. `If-Match: "3"` zmienia cenę tylko wtedy, gdy produkt ma wersję 3; inaczej `412` i nic nie jest zapisane. Chroni przed nadpisaniem zmiany innego klienta (optimistic concurrency).
- **Dokumentacja OpenAPI:** `/openapi/v1.json`, Swagger UI pod `/swagger` (w środowisku Development).

## Weryfikacja

**Kolekcja Postmana** jest w `postman/`:
1. Zaimportuj `Products.postman_collection.json` oraz środowiska z `postman/environments/` (4 warianty z compose i jedno lokalne).
2. Wybierz środowisko i uruchom folder **1. Scenariusz z zadania** (Run folder). Każde żądanie ma testy.
3. Folder **2. Walidacja i błędy** pokazuje odpowiedzi 400 i 404.
4. Folder **3. Idempotencja, współbieżność, health** (po kolei): `Idempotency-Key` (ponowienie i `422`), `If-Match` (`200` i `412`), `/health/live` i `/health/ready`.
5. Folder **4. Lista i usuwanie** (po kolei): stronicowanie `skip`/`take`, błędne stronicowanie (`400`), `DELETE` (`204`), odczyt i ponowne usunięcie (`404`).

**Przykładowa weryfikacja z zadania** (adres wariantu: `localhost:8081`):

| Krok | Jak sprawdzić |
|---|---|
| 9. Redis, Kafka i baza | `docker compose up -d` (PostgreSQL, Redis, Kafka); od razu z API w kontenerze: `docker compose --profile efcore-postgres up -d` |
| 10. Migracja inicjalna | `dotnet ef database update --project src/Products.Migrations.Postgres`; API w kontenerze zakłada schemat samo przy starcie (`docker logs api-efcore-postgres` → `Database schema ready (Migrations)`) |
| 11. API | `dotnet run --project src/Products.Api` (port 5032) albo kontener wariantu (porty 8081–8084); `GET /health` → `Healthy`, `variant`, stan bazy, Redisa i Kafki |
| 12. Utworzenie produktu | Postman 1.2: `201`, `Location` |
| 13. Pierwszy odczyt: z bazy, zapis do Redisa | Postman 1.3: `X-Cache: MISS`; `docker exec redis redis-cli GET product:<id>` i `TTL product:<id>` (~300 s) |
| 14. Drugi odczyt: z cache | Postman 1.4: `X-Cache: HIT` |
| 15–16. Zmiana ceny i unieważnienie cache | Postman 1.5; `docker exec redis redis-cli EXISTS product:<id>` → `0` |
| 17. Nowa cena | Postman 1.6: `X-Cache: MISS`, nowa cena |
| 18. Zdarzenie w logu konsumenta | konsola `dotnet run` albo `docker logs api-efcore-postgres` (PowerShell: `\| Select-String "Product price changed"`) |

Wpis w logu:
```
Product price changed: productId=..., oldPrice=149.99, newPrice=129.99, currency=PLN
```
Przy zmianie waluty dochodzi `oldCurrency=...`.

Gdy działa kilka wariantów naraz, każdy ma własną grupę konsumentów, więc loguje zdarzenia wszystkich wariantów, także tych z drugiej bazy. Zawartość topicu z nagłówkami (`event-type`, `traceparent`):
```bash
docker exec rekrutacja-kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:9092 --topic product-price-changed --from-beginning --property print.key=true --property print.headers=true
```

**Ślad zmiany ceny** w Aspire Dashboard (`--profile observability`, http://localhost:18888 → Traces): żądanie `PUT` z zapytaniami do bazy, wysyłka z outboxa (`product-price-changed publish`) i obsługa przez konsumentów każdego wariantu (`product-price-changed process`) w jednym śladzie. Metryki aplikacji (`products.*`): trafienia cache, wysłane i nieudane wiadomości outboxa, opóźnienie outbox → Kafka, odebrane zdarzenia.

## Testy

```bash
dotnet test
```

Uruchamia 117 testów kodu i sprawdzenie [bazy wiedzy](#baza-wiedzy) (po jednym przypadku na plik), razem w około 1 min. Testy integracyjne wymagają działającego Dockera.

- **Jednostkowe** (`tests/Products.UnitTests`, 34 testy) sprawdzają logikę z Core, bez infrastruktury:
  - scenariusze z zadania: cena ≤ 0, zmiana ceny nieistniejącego produktu, unieważnienie cache, poprzednia i nowa cena w zdarzeniu;
  - walidację i normalizację danych, cache-aside, kontrakt JSON zdarzenia i odświeżanie słownika walut;
  - `Idempotency-Key` (ponowienie, inna treść, niepoprawny klucz), konflikt wersji (`If-Match`), jedno zapytanie do bazy przy równoczesnych odczytach spoza cache, typ i ślad w wiadomości outboxa;
  - stronicowanie listy (domyślna strona, niepoprawne `skip`/`take`) i usuwanie z czyszczeniem cache.

  W tym samym projekcie `KnowledgeBundleTests` sprawdza bazę wiedzy: po jednym przypadku na plik (`type` i poprawny YAML w każdym pojęciu, istniejące linki i ścieżki `resource`), plus format `index.md` i `log.md`.
- **Integracyjne** (`tests/Products.IntegrationTests`, 83 testy) działają na Testcontainers, z własnymi kontenerami SQL Server, PostgreSQL, Redis i Kafka:
  - repozytoria w 4 wariantach, w tym 20 równoczesnych zmian ceny bez utraty żadnej, warunek wersji i 10 równoczesnych utworzeń z tym samym kluczem idempotencji (powstaje jeden produkt); lista od najnowszych ze stronicowaniem i usuwanie z historią cen i kluczem idempotencji;
  - outbox: kolejność, ponawianie, dead-letter po limicie błędów trwałych (błędy przejściowe nie zużywają prób), ślad żądania, blokada jednej instancji;
  - idempotentny konsument i retencja (usuwa tylko przeterminowane dane, dead-letter zostaje);
  - cache przy niedostępnym Redisie i przy uszkodzonym wpisie;
  - zgodność schematu: migracje EF i skrypty `db/` dają ten sam schemat i tę samą historię migracji (zapytania porównujące z bazy wiedzy), a obie drogi można przełączać na jednej bazie;
  - end-to-end przez HTTP dla 4 wariantów, na pustej bazie, na której schemat zakłada API przy starcie (jak w compose): API → baza → Redis → outbox → Kafka (nagłówki `event-type` i `traceparent` z trace-id żądania) → konsument, a także `Idempotency-Key`, `If-Match`, lista, usuwanie (z czyszczeniem Redisa) i frontend pod `/`.

**CI** (`.github/workflows/ci.yml`, GitHub Actions): format i analizatory (`dotnet format --verify-no-changes`), build Release, testy jednostkowe i integracyjne (Docker na runnerze), obraz Dockera.

## Benchmarki

Porównanie 4 wariantów na 100 tys. produktów z `db/*/Publish/benchmark.sql` ([wgranie danych](docs/knowledge/playbooks/load-data.md)), na dwóch poziomach:

- **Repozytoria** (BenchmarkDotNet, `benchmarks/Products.Benchmarks`): `GetById`, `Create` i `ChangePrice` w każdym wariancie, z alokacjami. Bez HTTP, cache i Kafki, więc wystarczą same bazy, bez kontenerów API.
  ```bash
  docker compose --profile postgres --profile sqlserver up -d
  dotnet run -c Release --project benchmarks/Products.Benchmarks
  ```
- **HTTP** (k6 w kontenerze, w sieci compose): przepustowość i opóźnienia API z bazą, jeden wariant naraz. Nakładka `benchmarks/docker-compose.benchmark.yml` wyłącza cache, Kafkę, telemetrię i logi `Information`.
  ```bash
  docker compose -f docker-compose.yml -f benchmarks/docker-compose.benchmark.yml --profile efcore-postgres up -d
  docker run --rm -i --network rekrutacja_default -e BASE_URL=http://api-efcore-postgres:8080 -e SCENARIO=read grafana/k6:2.3.0 run - < benchmarks/k6/products.js
  ```

Po pomiarach `dotnet run -c Release --project benchmarks/Products.Benchmarks -- cleanup` usuwa utworzone produkty i niewysłane zdarzenia (benchmark repozytoriów robi to sam), a `docker compose --profile "*" down` usuwa kontenery. Szczegóły: [instrukcja](docs/knowledge/playbooks/run-benchmarks.md).

**Wyniki** pierwszego pomiaru ([pełne tabele i przyczyny](docs/knowledge/references/benchmark-results.md)):
- **Odczyt i utworzenie:** EF Core i Dapper różnią się o kilka procent, a EF alokuje 2-5 razy więcej pamięci.
- **Zmiana ceny:** Dapper był szybszy o ok. 2 ms na operację, bo `SaveChanges` w jawnej transakcji zakładał automatyczny savepoint. Po jego wyłączeniu EF zrównał się z Dapperem w pojedynczej operacji. Pod pełnym obciążeniem HTTP na PostgreSQL Dapper wciąż obsługuje ok. 14% więcej żądań, bo EF zużywa więcej procesora.
- **PostgreSQL a SQL Server:** w Docker Desktop PostgreSQL jest szybszy od SQL Server, bo zapisy SQL Server ogranicza opróżnianie dziennika na dysk VM.

## Architektura

```
src/
  Products.Api                    Minimal API, DTO, walidacja → ProblemDetails, /health, Swagger, OpenTelemetry,
                                  frontend (wwwroot: grid produktów)
  Products.Core                   encje, ProductService (walidacja, cache-aside, idempotencja), CurrencyCatalog,
                                  zdarzenie ProductPriceChanged, ślady i metryki (ProductsTelemetry), kontrakty
  Products.Infrastructure         Redis (cache), Kafka (wysyłka outboxa, konsument)
  Products.Persistence            wspólne dla ORM: SqlDialect (SQL i typy parametrów per baza), outbox,
                                  ProcessedEvents i retencja (ADO.NET), ponawianie, health check bazy,
                                  schemat ze skryptów db/ (SchemaScripts)
  Products.Persistence.EfCore     DbContext, konfiguracje modelu, repozytoria EF
  Products.Persistence.Dapper     repozytoria Dappera
  Products.Migrations.SqlServer   migracje EF dla SQL Server
  Products.Migrations.Postgres    migracje EF dla PostgreSQL
tests/                            testy jednostkowe i integracyjne
benchmarks/                       BenchmarkDotNet (repozytoria), k6 (HTTP), nakładka compose do pomiarów
db/                               skrypty SQL (schemat, dane demo, dane do benchmarków)
docs/knowledge/                   baza wiedzy w formacie OKF (przepływy, mechanizmy, decyzje, instrukcje)
postman/                          kolekcja i środowiska Postmana
.github/workflows/                CI
```

### Odczyt produktu (cache-aside)

1. `GET product:{id}` z Redisa: przy trafieniu odpowiedź bez zapytania do bazy (`X-Cache: HIT`).
2. Przy braku wpisu odczyt z bazy i zapis do Redisa z TTL 5 min (`X-Cache: MISS`). Zapis do Redisa nie opóźnia odpowiedzi.
3. Równoczesne żądania o produkt spoza cache (np. popularny tuż po zmianie ceny) czekają na jeden odczyt z bazy zamiast wysyłać każde własne zapytanie (ochrona przed cache stampede, w obrębie instancji). Przy wyłączonym cache każde żądanie idzie do bazy, żeby nie zniekształcać benchmarków.
4. Nieistniejący produkt nie trafia do cache.

### Zmiana ceny

1. **Jedna transakcja w bazie:**
   - atomowy `UPDATE ... OUTPUT deleted.*` w SQL Server albo `UPDATE ... RETURNING old.*` w PostgreSQL 18 zwraca starą cenę; warunek `If-Match` (wersja) jest częścią tego samego `UPDATE`;
   - wpis w `ProductPriceHistory`;
   - zdarzenie w `OutboxMessages`, z typem (`ProductPriceChanged.v1`) i kontekstem śladu żądania (`traceparent`).

   UPDATE blokuje wiersz, więc równoczesne zmiany wykonują się po kolei i każda widzi poprzednią. Ten sam SQL wykonują EF Core (`SqlQueryRaw`) i Dapper.
2. **Usunięcie `product:{id}` z Redisa.**
3. **Wysyłka z outboxa** (`BackgroundService`): paczki w kolejności zapisu, wysyłane pod blokadą bazodanową (`sp_getapplock` / `pg_try_advisory_xact_lock`). Przy kilku instancjach aplikacji wysyła jedna naraz. Klucz wiadomości w Kafce to `ProductId`, więc zdarzenia jednego produktu trafiają do tej samej partycji. Nagłówki: `event-type` (typ z wersją schematu) i `traceparent`. Niedostępna Kafka nie blokuje API: zdarzenia czekają w outboxie.
   - **Dead-letter:** błąd samej wiadomości (np. za duża) zużywa próbę; po `Kafka:OutboxMaxAttempts` wiadomość jest pomijana (log `Error`, metryka `products.outbox.dead_lettered`), a kolejne nie czekają na nią w nieskończoność. Błędy przejściowe (brak brokera, timeout) prób nie zużywają. Ponowna wysyłka po naprawie przyczyny: `UPDATE OutboxMessages SET Attempts = 0 WHERE ProcessedAtUtc IS NULL AND Attempts >= 10`.
4. **Konsument** (`BackgroundService`) działa jako idempotentny inbox: w jednej transakcji rejestruje `(Consumer, EventId)` w `ProcessedEvents`, ponownie usuwa wpis z cache i zapisuje log. Duplikaty są pomijane, wiadomości o nieznanym `event-type` też. Pozycja w Kafce jest zapisywana dopiero po obsłużeniu (at-least-once). Nieudana obsługa jest ponawiana z rosnącym odstępem; po `Kafka:ConsumerMaxAttempts` zdarzenie jest pomijane z logiem `Error` (z treścią), żeby nie blokować partycji. Wpis cache wygaśnie wtedy najpóźniej po TTL.

### Decyzje projektowe

- **Słownik walut** `Currencies` ma klucz główny `Id`, a produkty odwołują się do unikalnego kodu ISO (`char(3)` / `varchar(3)`), więc odczyt produktu nie wymaga złączenia. Walidacja korzysta z `CurrencyCatalog`: aktywne waluty są trzymane w pamięci (`FrozenSet`) i odświeżane co 5 min.
- **Uczciwe porównanie wariantów:**
  - te same sterowniki baz w Dapperze i EF;
  - typy parametrów dopasowane do kolumn: stała precyzja `decimal(18,2)` (inaczej SQL Server tworzyłby osobny plan dla każdej liczby cyfr w kwocie) i `datetime2` zamiast `datetime`;
  - EF korzysta z puli kontekstów i zapytania kompilowanego raz, a w transakcji zmiany ceny nie zakłada automatycznego savepointu (zbędne wymiany z bazą);
  - JSON jest generowany przy kompilacji, logi przez `[LoggerMessage]` (bez alokacji, gdy poziom jest wyłączony);
  - ta sama polityka ponawiania przy błędach przejściowych bazy (zerwane połączenie, deadlock): EF Core `EnableRetryOnFailure`, Dapper `DbRetry`, po 3 ponowienia;
  - SQL Server z `READ_COMMITTED_SNAPSHOT` (odczyty nie czekają na blokady zapisów), czyli ta sama semantyka co MVCC w PostgreSQL. EF Core włącza tę opcję sam, gdy tworzy bazę; skrypt `db/sqlserver/Create/000_database.sql` też. Dla bazy utworzonej inaczej: `ALTER DATABASE Products SET READ_COMMITTED_SNAPSHOT ON`.
- **Schema:** źródłem prawdy są migracje EF. Skrypty w `db/` dają identyczny schemat i zapisują tę samą historię migracji EF, więc obie drogi są zamienne na jednej bazie. Pilnuje tego test `SchemaSourcesTests`, który porównuje katalogi systemowe obu baz.
  - `Create/` zawiera schemat, `Demo/` 10 produktów demo, a `Benchmark/` generator 100 tys. produktów;
  - API może zakładać schemat przy starcie z migracji albo ze skryptów `Create/` wbudowanych w `Products.Persistence` (`Database:Schema`). Skrypty SQL Server są dzielone po `GO`, a `000_database.sql` (zakłada bazę, wymaga `sqlcmd`/`psql`) jest pomijany;
  - `db/merge_files.cmd` scala je do `Publish/*.sql`.

  Wgranie danych demo (cmd lub bash; w Git Bash poprzedź polecenia `MSYS_NO_PATHCONV=1`, inaczej ścieżki `/opt/...` i `/dev/stdin` zostaną zamienione na ścieżki Windows):
  ```bash
  docker exec -i postgres psql -U postgres -d products < db/postgres/Publish/demo.sql
  docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P LocalDev_Passw0rd -C -I -i /dev/stdin < db/sqlserver/Publish/demo.sql
  ```
- **Odporność:**
  - Redis bez połączenia od razu kończy polecenie błędem, więc odczyt idzie do bazy, a `/health` pokazuje `Degraded`;
  - Redis w compose nie zapisuje danych na dysk (po restarcie nie przywraca nieaktualnych wpisów) i ma limit pamięci 256 MB z wypieraniem najdawniej używanych kluczy (`allkeys-lru`);
  - Kafka trzyma topic i offsety na wolumenie (`KAFKA_LOG_DIRS`), więc przetrwają odtworzenie kontenera;
  - Kafka niedostępna oznacza `Degraded`, a zdarzenia czekają w outboxie;
  - health checki mają limity czasu (baza 5 s, Redis i Kafka 3 s); `/health/live` nie sprawdza zależności, więc awaria bazy nie powoduje restartu instancji;
  - kontenery mają czas na łagodne zamknięcie (`stop_grace_period`): API zatwierdza offsety Kafki i kończy wysyłkę, bazy i Kafka zapisują stan.
- **Retencja** (`BackgroundService`, co godzinę, paczkami po 1000 wierszy): wysłane wiadomości outboxa i obsłużone zdarzenia po 7 dniach (tyle domyślnie Kafka trzyma wiadomości), klucze idempotencji po 1 dniu. Dead-letter zostaje do ręcznej obsługi.
- **Obserwowalność (OpenTelemetry):** ślady HTTP, zapytań SQL (SqlClient, Npgsql) oraz wysyłki i obsługi zdarzeń, propagowane przez outbox i nagłówek Kafki `traceparent`; metryki ASP.NET Core, runtime i aplikacji (`products.*`); logi skorelowane ze śladami. Zapytania pętli w tle bez nadrzędnego żądania (outbox co sekundę, retencja) nie trafiają do śladów. Bez `OTEL_EXPORTER_OTLP_ENDPOINT` OpenTelemetry nie jest rejestrowane.
- **Obraz Dockera:** `aspnet:10.0.13-noble-chiseled-extra`, bez powłoki i menedżera pakietów, z użytkownikiem bez uprawnień administratora. Wariant `extra` zawiera ICU, wymagane przez SqlClient; bibliotek Kerberos nie ma, więc połączenie z PostgreSQL w compose ma `GSS Encryption Mode=Disable`. Wszystkie obrazy (SDK, runtime, bazy, Kafka, Redis, dashboard) mają przypięte wersje, także w testach.
- **Jakość kodu:** analizatory .NET (`AnalysisLevel=latest-recommended`), ostrzeżenia jako błędy, styl w `.editorconfig`; wyjątki są opisane w `.editorconfig`.

## Baza wiedzy

`docs/knowledge/` opisuje projekt w formacie [Open Knowledge Format](https://github.com/GoogleCloudPlatform/open-knowledge-format) (OKF v0.2): przepływy, mechanizmy, decyzje z uzasadnieniem, kontrakty, instrukcje i pułapki. Jeden plik to jedno pojęcie z nagłówkiem YAML; pliki łączą się linkami, więc da się je czytać po kolei (człowiek) albo dociągać tylko potrzebne (agent AI). Schematów nie powtarza - linkuje do kodu, migracji i OpenAPI.

Wejście: [docs/knowledge/index.md](docs/knowledge/index.md). Test `KnowledgeBundleTests` pilnuje zgodności ze specyfikacją i aktualności odwołań: zmiana nazwy pliku w kodzie bez poprawki dokumentacji psuje build.

## Znane ograniczenia i możliwe rozszerzenia produkcyjne

- **Wyścig cache-aside:** odczyt z bazy przed zmianą ceny może zapisać do cache starą wartość już po jej unieważnieniu. Drugie unieważnienie (konsument zdarzenia) zwykle to naprawia, a w najgorszym razie nieaktualność trwa najwyżej TTL. Pełnym rozwiązaniem byłby zapis do cache warunkowy po `Version` (skrypt Lua).
- **Dead-letter jako tabela:** wiadomości po limicie prób zostają w `OutboxMessages` (log `Error`, metryka). W większym systemie byłby topic `*.DLT` z alertem i narzędziem do ponownej wysyłki.
- **Klucze idempotencji są globalne:** bez uwierzytelniania nie ma do kogo ich przypisać. Z uwierzytelnianiem klucz powinien być unikalny w obrębie klienta.
- **Schemat zakładany przy starcie** jest wygodny lokalnie. Na produkcji powinien to robić osobny krok wdrożenia (np. `dotnet ef migrations bundle` albo uruchomienie skryptów), a nie każda instancja API.
- **Bezpieczeństwo:** uwierzytelnianie, HTTPS, sekrety w menedżerze sekretów zamiast konfiguracji, osobny użytkownik bazy z minimalnymi uprawnieniami (bez `sa` / `postgres`), Aspire Dashboard z logowaniem.
- **Stronicowanie listy przez `skip`/`take`** pozwala skoczyć do dowolnej strony, ale baza pomija `skip` wierszy, więc dalekie strony są wolniejsze, a dopisane w międzyczasie produkty przesuwają strony. Przy dużych tabelach lepsze jest stronicowanie po kluczu (`CreatedAtUtc`, `Id` ostatniego wiersza) z „następna/poprzednia” zamiast numerów stron.
- **Frontend** to statyczna strona bez uwierzytelniania i bez testów UI (sprawdzony ręcznie; serwowanie strony sprawdza test end-to-end).
