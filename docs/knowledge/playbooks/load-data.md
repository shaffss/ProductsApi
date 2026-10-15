---
type: Playbook
title: Dane demo i do benchmarków
description: "Wgrywanie 10 produktów demo i 100 tys. produktów do benchmarków skryptami z db/*/Publish."
resource: ../../../db/
tags: [database, data, benchmarks]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

| Skrypt | Zawartość |
|---|---|
| `db/<baza>/Publish/create.sql` | schemat, taki sam jak z migracji |
| `db/<baza>/Publish/demo.sql` | 10 produktów demo (Id `00000000-0000-0000-0000-000000000001` do `...0010`) z historią cen |
| `db/<baza>/Publish/benchmark.sql` | 100 tys. produktów `Bench ...` (Id `b0000000-0000-0000-0000-{n w hex}`), historia co 10. produktu, aktualizacja statystyk |

```bash
docker exec -i postgres psql -U postgres -d products < db/postgres/Publish/demo.sql
docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P LocalDev_Passw0rd -C -I -i /dev/stdin < db/sqlserver/Publish/demo.sql
```

- **`sqlcmd` potrzebuje `-I`** - [pułapka](../pitfalls/sqlcmd-quoted-identifier.md). W Git Bash poprzedź polecenia `MSYS_NO_PATHCONV=1`, inaczej ścieżki `/opt/...` i `/dev/stdin` zostaną zamienione na ścieżki Windows.
- **Idempotentne:** ponowne uruchomienie niczego nie dubluje. Generator benchmarkowy dogenerowuje tylko brakujące numery.
- **Wstawianie po kolei:** `GENERATE_SERIES` z jawnym krokiem `1` (przy start > stop domyślnie liczy w dół), bez złączenia. Wstawianie z `NOT EXISTS` (hash anti-join) mieszało kolejność i dzieliło strony indeksu (wypełnienie ok. 58%).
- **Usunięcie danych benchmarkowych:** polecenia w komentarzu na początku `db/<baza>/Benchmark/001_products.sql`.
- **Źródła** są w `db/<baza>/Demo` i `db/<baza>/Benchmark`; po zmianie uruchom `.\db\merge_files.cmd`.

Powiązane: [Uczciwe porównanie wariantów](../decisions/fair-comparison.md)
