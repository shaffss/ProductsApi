---
type: Playbook
title: Benchmarki wariantów
description: "Pomiar 4 wariantów: repozytoria w BenchmarkDotNet i API przez HTTP w k6, na danych benchmarkowych, z porządkiem po pomiarach."
resource: ../../../benchmarks/
tags: [benchmarks, performance, ef-core, dapper]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-16T05:40:00Z }
---

Najpierw [wgraj dane benchmarkowe](load-data.md) do obu baz (`benchmark.sql`, 100 tys. produktów). Bez nich pomiar od razu kończy się błędem.

# Repozytoria (BenchmarkDotNet)

```bash
docker compose --profile postgres --profile sqlserver up -d                               # same bazy, bez API
dotnet run -c Release --project benchmarks/Products.Benchmarks                          # ok. 15 min
dotnet run -c Release --project benchmarks/Products.Benchmarks -- --filter "*GetById*"  # jedna operacja
dotnet run -c Release --project benchmarks/Products.Benchmarks -- --job short           # szybciej, mniej dokładnie
```

- **Mierzone operacje:** `GetById`, `Create` i `ChangePrice` (atomowy `UPDATE`, historia i outbox w jednej transakcji). Są to te same metody repozytoriów, których używa API, ale bez HTTP, cache i Kafki. `[MemoryDiagnoser]` podaje też alokacje.
- **Osobne procesy:** każda kombinacja `Orm` × `Database` działa w osobnym procesie, więc pule połączeń i JIT jednego wariantu nie wpływają na inne.
- **Te same dane w każdym wariancie:** Id są wyliczane jak w generatorze (`b0000000-0000-0000-0000-{n w hex}`), a kolejne wywołania przechodzą po produktach z krokiem 7919, w tej samej kolejności.
- **Zmiana ceny jest zawsze prawdziwa:** cena jest losowa i spoza zakresu danych (1-999.99), więc każde wywołanie zapisuje historię i outbox, a nie kończy się na „bez zmian”.
- **Bez kontenerów API:** działające API wysyłałoby z outboxa zdarzenia z pomiaru, a jego konsument zapisywałby je w `ProcessedEvents`. To dodatkowe obciążenie baz w trakcie pomiaru.
- **Połączenia** domyślnie idą do baz z compose na `localhost`; inne bazy ustawisz zmiennymi `ConnectionStrings__Postgres` / `ConnectionStrings__SqlServer`.
- **Wyniki** trafiają do `BenchmarkDotNet.Artifacts/` w bieżącym katalogu (ignorowany przez git).

# HTTP (k6)

```bash
docker compose -f docker-compose.yml -f benchmarks/docker-compose.benchmark.yml --profile efcore-postgres up -d
docker run --rm -i --network rekrutacja_default -e BASE_URL=http://api-efcore-postgres:8080 -e SCENARIO=read grafana/k6:2.3.0 run - < benchmarks/k6/products.js
docker compose --profile "*" down    # przed kolejnym wariantem
```

- **Jeden wariant naraz:** profil uruchamia jego API, bazę, Redisa i Kafkę, a `BASE_URL` wskazuje kontener API (`api-<profil>:8080`, np. `api-dapper-sqlserver:8080`).
- **Scenariusze** (`SCENARIO`): `read` (GET losowego produktu), `write` (PUT ceny), `create` (POST). Do tego `VUS` (domyślnie 32) i `DURATION` (30s).
- **k6 działa w sieci compose**, nie przez porty 8081-8084. Przekierowanie portów Docker Desktop dokłada opóźnienie, które zakryłoby różnice między wariantami.
- **Nakładka** `benchmarks/docker-compose.benchmark.yml`:
  - wyłącza cache, Kafkę, OpenTelemetry i logi `Information` we wszystkich wariantach jednakowo;
  - outbox jest dalej zapisywany (jest częścią zmiany ceny), ale nie jest wysyłany.
- **Ważność pomiaru:** próg `checks: rate==1` oznacza, że już jedna błędna odpowiedź unieważnia wynik (k6 kończy się kodem błędu).

# Po pomiarach

```bash
dotnet run -c Release --project benchmarks/Products.Benchmarks -- cleanup
docker compose --profile "*" down
```

- **`cleanup`** usuwa produkty `Benchmark create` i niewysłane zdarzenia produktów benchmarkowych z działających baz (niedostępną pomija). Bez tego API po włączeniu Kafki wysłałoby tysiące zdarzeń z pomiarów.
- **Benchmark repozytoriów sprząta sam** (`GlobalCleanup`); po k6 uruchom `cleanup` przed `down`, póki działa baza wariantu.
- **Historia cen i nowe ceny produktów `Bench` zostają.** Nie wpływają na kolejne pomiary.

# Interpretacja

- **Docker Desktop:** bazy działają w maszynie wirtualnej Dockera, a BenchmarkDotNet ostrzega o Hyper-V. Wyniki bezwzględne zawierają narzut sieci i dysku VM, więc porównuj warianty między sobą, mierzone w tych samych warunkach.
- **Zapis na SQL Server** czeka na zapis dziennika transakcji (każdy commit), więc jego czas zależy głównie od dysku.

Powiązane: [Wyniki benchmarków](../references/benchmark-results.md), [Uczciwe porównanie wariantów](../decisions/fair-comparison.md), [Dane demo i do benchmarków](load-data.md)
