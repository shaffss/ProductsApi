# Architektura i porównanie wariantów

* [Cztery warianty dostępu do danych](four-variants.md) - EF Core albo Dapper, każdy dla PostgreSQL albo SQL Server, wybierane konfiguracją; reszta kodu wspólna, żeby benchmarki porównywały tylko ORM i bazę.
* [Uczciwe porównanie wariantów](fair-comparison.md) - Warianty różnią się tylko ORM i bazą: ten sam SQL zmiany ceny, te same sterowniki i typy parametrów, bez narzutów fałszujących pomiary.

# Zasady biznesowe

* [Produkty nie są usuwane](no-product-deletion.md) - Usuwanie produktów jest wyłączone w całym systemie (decyzja biznesowa) - kod zakomentowany, a DELETE /api/products/{id} zwraca 405.

# Baza danych

* [Migracje EF jako źródło prawdy schematu](migrations-source-of-truth.md) - Schemat obu baz pochodzi z migracji EF Core; skrypty w db/ dają ten sam schemat i historię migracji (test), więc API może zakładać schemat jedną albo drugą drogą.
* [READ_COMMITTED_SNAPSHOT zamiast READPAST](rcsi-not-readpast.md) - Odczyt outboxa w SQL Server nie czeka na niezatwierdzone transakcje dzięki wersjonowaniu wierszy, a nie hintowi READPAST.
* [Słownik walut z kluczem po kodzie](currency-dictionary.md) - Waluty w tabeli Currencies (klucz Id, unikalny kod), produkty odwołują się do kodu ISO - odczyt bez złączenia, walidacja z pamięci.

# Infrastruktura

* [Redis jako czysty cache](redis-pure-cache.md) - StackExchange.Redis bezpośrednio, bez zapisu na dysk i z limitem pamięci LRU - baza jest źródłem prawdy, Redis może zgubić wszystko.
* [Obraz Dockera i przypięte wersje](container-image.md) - Runtime aspnet chiseled-extra bez powłoki, użytkownik bez uprawnień, przypięte wersje obrazów i czas na łagodne zamknięcie.
