# Historia zmian

## 2026-10-16
* **Aktualizacja**: grid z przełącznikiem trybu jasnego i ciemnego - [lista, usuwanie i grid](flows/list-and-delete.md).
* **Aktualizacja**: launcher z opcją Aspire Dashboard (uruchomienie kontenera, bez niej API bez telemetrii) i linkami w statusie kontenerów (UI w przeglądarce albo adres do schowka); po starcie okno zostaje otwarte jako panel statusu, a przy API w kontenerze pozwala przebudować i uruchomić wariant ponownie - [uruchomienie lokalne](playbooks/run-locally.md).
* **Utworzenie**: pułapka [zawieszonego startu kontenera z plikiem z Windows](pitfalls/docker-desktop-bind-mount.md) - `sqlserver-init` bez montowania pliku, limit czasu w launcherze ([uruchomienie lokalne](playbooks/run-locally.md)).
* **Utworzenie**: [lista, usuwanie i grid produktów](flows/list-and-delete.md) - GET ze stronicowaniem skip/take, DELETE, frontend pod `/`; aktualizacja [kontraktu HTTP](contracts/http-api.md), [schematu bazy](contracts/database-schema.md) (indeks listy) i [decyzji o migracjach](decisions/migrations-source-of-truth.md) (druga migracja).
* **Aktualizacja**: launcher uruchamia kontenery wybranego wariantu ([uruchomienie lokalne](playbooks/run-locally.md)).
* **Aktualizacja**: schemat przy starcie z migracji EF albo ze skryptów `db/` (`Database:Schema`), zamienne na jednej bazie dzięki historii migracji w skryptach i pilnowane testem - [decyzja](decisions/migrations-source-of-truth.md), [zmiana schematu](playbooks/schema-change.md), [schemat bazy](contracts/database-schema.md), [uruchomienie lokalne](playbooks/run-locally.md).
* **Aktualizacja**: Compose uruchamia minimum jednego wariantu (profile), bez polityki restartu - [uruchomienie lokalne](playbooks/run-locally.md), [benchmarki](playbooks/run-benchmarks.md), [cztery warianty](decisions/four-variants.md); nowa pułapka [down a profile](pitfalls/compose-down-profiles.md).
* **Aktualizacja**: zmiana ceny w EF bez automatycznego savepointu - ponowny pomiar w [wynikach](references/benchmark-results.md), zasada w [uczciwym porównaniu](decisions/fair-comparison.md).
* **Utworzenie**: [wyniki benchmarków](references/benchmark-results.md) - pierwszy pomiar 4 wariantów z przyczynami różnic (automatyczny savepoint EF, opróżnianie dziennika SQL Server).
* **Utworzenie**: [instrukcja benchmarków](playbooks/run-benchmarks.md) - BenchmarkDotNet na repozytoriach, k6 przez HTTP z nakładką compose i sprzątanie po pomiarach; link w [uczciwym porównaniu](decisions/fair-comparison.md).
* **Aktualizacja**: po sprawdzeniu zgodności z PDF i świeżym uruchomieniu na pustych wolumenach - nowe pułapki [równoległego tworzenia bazy](pitfalls/concurrent-database-creation.md), [Kerberosa w obrazie chiseled](pitfalls/chiseled-gssapi.md) i [danych Kafki poza wolumenem](pitfalls/kafka-log-dirs.md); sekwencja uruchomienia z zadania w [instrukcji](playbooks/run-locally.md); zapis ceny w [kontrakcie HTTP](contracts/http-api.md).

## 2026-10-15
* **Utworzenie**: baza wiedzy w formacie OKF v0.2 z notatek projektu, README i kodu - [przepływy](flows/), [mechanizmy](mechanisms/), [decyzje](decisions/), [kontrakty](contracts/), [instrukcje](playbooks/) i [pułapki](pitfalls/).
