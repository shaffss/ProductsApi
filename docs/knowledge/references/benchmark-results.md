---
type: Benchmark Report
title: Wyniki benchmarków
description: "Pierwszy pomiar 4 wariantów (2026-10-16): repozytoria w BenchmarkDotNet i API w k6, z przyczynami różnic."
resource: ../../../benchmarks/
tags: [benchmarks, performance, ef-core, dapper, results]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-16T06:40:00Z }
---

Metodologia i uruchamianie: [Benchmarki wariantów](../playbooks/run-benchmarks.md).

# Środowisko

- **Host:** Intel Core i9-13900HX (BenchmarkDotNet widzi 8 rdzeni / 16 wątków pod Hyper-V), Windows 11 24H2.
- **Docker Desktop 29.1.3:** backend Hyper-V, 6 GB pamięci dla VM. Bazy i API działają w tej VM, a benchmark repozytoriów na hoście (.NET 10.0.12), więc każde zapytanie przechodzi przez przekierowanie portów (ok. 1.2 ms na wymianę).
- **Bazy:** PostgreSQL 18.6 i SQL Server 2025 CU9, 100 tys. produktów z `benchmark.sql`.

# Repozytoria (BenchmarkDotNet, domyślny job)

Średni czas jednej operacji i alokacje:

| Operacja | EF + PostgreSQL | Dapper + PostgreSQL | EF + SQL Server | Dapper + SQL Server |
|---|---|---|---|---|
| GetById | 1.26 ms / 8.3 KB | 1.27 ms / 3.7 KB | 1.48 ms / 11.4 KB | 1.43 ms / 7.5 KB |
| Create | 3.65 ms / 18.2 KB | 3.44 ms / 4.2 KB | 7.66 ms / 22.6 KB | 7.53 ms / 8.3 KB |
| ChangePrice (z savepointem EF) | 8.57 ms / 51.8 KB | 6.23 ms / 11.3 KB | 14.86 ms / 59.2 KB | 12.80 ms / 17.4 KB |
| **ChangePrice (bez savepointu)** | **6.31 ms** / 49.6 KB | 6.42 ms / 11.5 KB | **13.19 ms** / 57.1 KB | 12.54 ms / 17.4 KB |

Wiersz „bez savepointu” zmierzono ponownie dla wszystkich wariantów po zmianie w EF, w tej samej sesji (`--filter "*ChangePrice*"`).

# API przez HTTP (k6)

32 wirtualnych użytkowników, 30 s pomiaru po 5 s rozgrzewki, k6 w sieci compose, nakładka benchmarkowa. Żądania na sekundę i p95:

| Scenariusz | EF + PostgreSQL | Dapper + PostgreSQL | EF + SQL Server | Dapper + SQL Server |
|---|---|---|---|---|
| read (GET) | 12 248/s, 5.2 ms | 12 892/s, 5.0 ms | 8 043/s, 7.3 ms | 8 061/s, 7.3 ms |
| write (z savepointem EF) | 2 311/s, 19.3 ms | 2 763/s, 16.2 ms | 533/s, 96.7 ms | 540/s, 98.1 ms |
| **write (bez savepointu)** | **2 481/s, 18.8 ms** | 2 829/s, 15.7 ms | 579/s, 89.4 ms | 519/s, 119.3 ms |
| create (POST) | 3 457/s, 13.2 ms | 3 597/s, 12.9 ms | 1 905/s, 25.0 ms | 1 908/s, 25.2 ms |

Wszystkie odpowiedzi poprawne (próg `checks: rate==1`).

# Wnioski

- **Odczyt i utworzenie:** EF i Dapper są w granicach kilku procent. O czasie decyduje wymiana z bazą, a nie ORM. EF alokuje 2-5 razy więcej pamięci.
- **Zmiana ceny była wolniejsza w EF przez automatyczny savepoint.** Różnica wynosiła 2.3 ms na PostgreSQL i 2.1 ms na SQL Server w repozytorium, a przez HTTP Dapper obsługiwał na PostgreSQL o 20% więcej żądań na sekundę.
  - **Przyczyna:** `SaveChanges` w jawnej transakcji zakłada savepoint. Potwierdzają to logi transakcji EF.
  - **PostgreSQL:** `SAVEPOINT` przed zapisem outboxa i `RELEASE SAVEPOINT` po nim, czyli dwie dodatkowe wymiany z bazą.
  - **SQL Server:** sam `SAVE TRANSACTION`.
  - **Dapper:** wykonuje tylko polecenia z [atomowej zmiany ceny](../mechanisms/atomic-price-change.md).
- **Po wyłączeniu savepointu** (`Database.AutoSavepointsEnabled = false` w `EfProductRepository.ChangePriceAsync`; błąd i tak wycofuje całą transakcję) EF zrównał się z Dapperem w pojedynczej operacji.
  - **Przez HTTP na PostgreSQL** Dapper nadal obsługuje o ok. 14% więcej żądań.
  - **Powód:** przy 32 równoczesnych użytkownikach k6, API i bazy dzielą procesor jednej VM. Przy takim obciążeniu liczy się dodatkowa praca procesora w EF (śledzenie zmian, potok `SaveChanges`, 4 razy więcej alokacji).
- **PostgreSQL jest szybszy od SQL Server w tym środowisku:** odczyty 1.5 razy, zapisy 2-5 razy. Zapisy SQL Server ograniczają głównie dwie rzeczy (`sys.dm_os_wait_stats` po pomiarach):
  - opróżnianie dziennika na dysk VM: `PREEMPTIVE_OS_FLUSHFILEBUFFERS` i `WRITELOG`;
  - rywalizacja o ostatnią stronę indeksów z kluczem identity (`OutboxMessages`, `ProductPriceHistory`): `PAGELATCH_EX`.
- **Przy zmianie ceny na SQL Server oba ORM są równe przez HTTP** (520-580/s, różnice w granicach rozrzutu między przebiegami), bo wąskim gardłem jest baza, a nie liczba wymian.

# Do sprawdzenia

- `OPTIMIZE_FOR_SEQUENTIAL_KEY = ON` dla indeksów z kluczem identity w SQL Server (`PAGELATCH_EX`).
- Pomiar na Linuksie bez Docker Desktop: bez przekierowania portów i wirtualnego dysku VM liczby bezwzględne będą inne.

Powiązane: [Uczciwe porównanie wariantów](../decisions/fair-comparison.md)
