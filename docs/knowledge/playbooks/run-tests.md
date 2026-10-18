---
type: Playbook
title: Testy, format i CI
description: "Testy jednostkowe i integracyjne (Testcontainers), format i analizatory jak w CI."
resource: ../../../.github/workflows/ci.yml
tags: [tests, ci, quality]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

```bash
dotnet test                                          # wszystkie testy, ok. 1 min, integracyjne wymagają Dockera
dotnet test --project tests/Products.UnitTests       # tylko jednostkowe, bez Dockera
dotnet format Products.slnx --verify-no-changes      # styl i analizatory, jak w CI
```

# Testy integracyjne

- Stawiają własne kontenery (SQL Server, PostgreSQL, Redis, Kafka) raz na przebieg (`AssemblyFixture`), w tych samych przypiętych wersjach co compose.
- **Pamięć Dockera:** testowy SQL Server działa obok kontenerów z compose. Przy 6 GB warto je na czas testów usunąć: `docker compose --profile "*" down`.
- **Klasy testów idą po kolei** (`Parallelization(Mode = ParallelMode.None)`), bo równolegle przy małej pamięci SQL Server łapał timeouty. Równoległość wewnątrz testów, np. współbieżne zmiany cen, zostaje.
- **Każdy test end-to-end** ma własną bazę i grupę konsumentów Kafki. Wspólna grupa i twardo zabity proces blokowały partycje na czas `session.timeout` (45 s).

# Jakość

- xUnit v3 działa przez Microsoft.Testing.Platform (`global.json`: `test.runner`).
- Ostrzeżenia są błędami (`AnalysisLevel=latest-recommended`); wyjątki z uzasadnieniem są w `.editorconfig`.
- `KnowledgeBundleTests` sprawdza tę bazę wiedzy: pole `type` w każdym pojęciu oraz istniejące linki i ścieżki `resource`.
- CI (GitHub Actions): restore, format, build Release, testy jednostkowe i integracyjne (Docker na runnerze), `docker build`. Runner przypięty do `ubuntu-24.04` - zmiana obrazu `ubuntu-latest` nie zmienia wyniku testów bez zapowiedzi.
- Testy nie zależą od rozdzielczości zegara bazy: SQL Server na runnerze potrafi dać ten sam znacznik czasu dla dwóch zapisów w ciągu kilku milisekund, więc test porównujący daty najpierw cofa datę w bazie (`TestDatabase.SetDateAsync`).

Powiązane: [Uruchomienie lokalne](run-locally.md)
