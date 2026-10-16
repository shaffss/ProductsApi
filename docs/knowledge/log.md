# Historia zmian

## 2026-10-16
* **Aktualizacja**: Compose uruchamia minimum jednego wariantu (profile), bez polityki restartu - [uruchomienie lokalne](playbooks/run-locally.md), [benchmarki](playbooks/run-benchmarks.md), [cztery warianty](decisions/four-variants.md); nowa pułapka [down a profile](pitfalls/compose-down-profiles.md).
* **Aktualizacja**: zmiana ceny w EF bez automatycznego savepointu - ponowny pomiar w [wynikach](references/benchmark-results.md), zasada w [uczciwym porównaniu](decisions/fair-comparison.md).
* **Utworzenie**: [wyniki benchmarków](references/benchmark-results.md) - pierwszy pomiar 4 wariantów z przyczynami różnic (automatyczny savepoint EF, opróżnianie dziennika SQL Server).
* **Utworzenie**: [instrukcja benchmarków](playbooks/run-benchmarks.md) - BenchmarkDotNet na repozytoriach, k6 przez HTTP z nakładką compose i sprzątanie po pomiarach; link w [uczciwym porównaniu](decisions/fair-comparison.md).
* **Aktualizacja**: po sprawdzeniu zgodności z PDF i świeżym uruchomieniu na pustych wolumenach - nowe pułapki [równoległego tworzenia bazy](pitfalls/concurrent-database-creation.md), [Kerberosa w obrazie chiseled](pitfalls/chiseled-gssapi.md) i [danych Kafki poza wolumenem](pitfalls/kafka-log-dirs.md); sekwencja uruchomienia z zadania w [instrukcji](playbooks/run-locally.md); zapis ceny w [kontrakcie HTTP](contracts/http-api.md).

## 2026-10-15
* **Utworzenie**: baza wiedzy w formacie OKF v0.2 z notatek projektu, README i kodu - [przepływy](flows/), [mechanizmy](mechanisms/), [decyzje](decisions/), [kontrakty](contracts/), [instrukcje](playbooks/) i [pułapki](pitfalls/).
