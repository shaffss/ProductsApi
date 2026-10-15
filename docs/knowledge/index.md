---
okf_version: "0.2"
---

# Products API - baza wiedzy

Wiedza o projekcie w formacie [Open Knowledge Format v0.2](https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/SPEC.md): jeden plik opisuje jedno pojęcie (nagłówek YAML z polem `type`), a pojęcia łączą się zwykłymi linkami. Opisuje to, czego nie widać w kodzie: przepływy, mechanizmy, decyzje z uzasadnieniem, instrukcje i pułapki. Kolumny, pola i klucze konfiguracji są w migracjach, OpenAPI i `appsettings.json` - pojęcia tylko do nich linkują.

Dla recenzenta zadania punktem wejścia jest [README](../../README.md).

# Na początek

* [Zmiana ceny](flows/change-price.md) - główny scenariusz zadania: od PUT przez outbox i Kafkę do logu konsumenta.
* [Odczyt produktu](flows/read-product.md) - cache-aside w Redisie.
* [Utworzenie produktu](flows/create-product.md) - walidacja i opcjonalny klucz idempotencji.

# Katalogi

* [Przepływy](flows/) - scenariusze od żądania HTTP do bazy, cache i Kafki.
* [Mechanizmy](mechanisms/) - jak działają zapis, cache, outbox, inbox, idempotencja, retencja i telemetria.
* [Decyzje](decisions/) - wybory projektowe z powodami i konsekwencjami.
* [Kontrakty](contracts/) - API HTTP, zdarzenie w Kafce i schemat bazy.
* [Instrukcje](playbooks/) - uruchamianie, testy, zmiana schematu, dead-letter, dane.
* [Pułapki](pitfalls/) - problemy napotkane w projekcie i ich rozwiązania.
* [Materiały pomocnicze](references/) - zapytania do porównania schematów baz.
* [Historia zmian](log.md) - zmiany w tej bazie wiedzy.

# Konwencje

* Linki są względne: baza leży w podkatalogu repozytorium, więc działają w GitHubie i w IDE. Ścieżki w `resource` też są względne do pliku pojęcia.
* `type` jest po angielsku (Flow, Mechanism, Decision, API, Event, Database Schema, Playbook, Pitfall), treść po polsku.
* Treść przygotował agent (`generated`). Po przeglądzie dopisz `verified: { by: human:<login>, at: <czas UTC> }` - pojęcie przejdzie z poziomu „unverified” na „human-reviewed”.
* Spójność sprawdza test `KnowledgeBundleTests`: pole `type` w każdym pojęciu, istniejące linki i ścieżki `resource`.
