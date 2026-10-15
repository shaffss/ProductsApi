# Zapis i spójność danych

* [Atomowa zmiana ceny](atomic-price-change.md) - Jedno polecenie SQL zmienia cenę, zwraca poprzednią i zapisuje historię - równoczesne zmiany nie gubią się.
* [Optymistyczna współbieżność (ETag / If-Match)](optimistic-concurrency.md) - ETag to wersja produktu; PUT z If-Match zmienia cenę tylko przy tej wersji, inaczej 412.
* [Idempotency-Key](idempotency-key.md) - Ponowiony POST z tym samym kluczem zwraca produkt utworzony za pierwszym razem; ten sam klucz z inną treścią to 422.
* [Ponawianie przy błędach przejściowych bazy](db-retries.md) - Zerwane połączenie, deadlock i chwilowa niedostępność bazy są ponawiane do 3 razy - ta sama polityka w EF Core i Dapperze.
* [Walidacja i błędy](validation.md) - Walidacja w ProductService zwraca Result<T> zamiast wyjątków; błędy jako ProblemDetails z listą pól, także dla niepoprawnego JSON.

# Cache

* [Cache-aside z ochroną przed stampede](cache-aside.md) - Redis jako cache odczytu produktu: klucz product:{id} z TTL, podwójne unieważnienie i jeden odczyt z bazy przy równoczesnych żądaniach.

# Zdarzenia

* [Transactional Outbox z dead-letter](outbox.md) - Zdarzenia zapisywane w transakcji zmiany danych i wysyłane do Kafki w tle: kolejność per produkt, at-least-once, dead-letter po limicie prób.
* [Idempotentny konsument (inbox)](inbox.md) - Konsument ProductPriceChanged obsługuje zdarzenie raz na odbiorcę: rejestracja w ProcessedEvents w transakcji obsługi, ograniczone ponowienia.

# Utrzymanie

* [Retencja danych technicznych](retention.md) - Co godzinę usuwa paczkami wysłane wiadomości outboxa, obsłużone zdarzenia i stare klucze idempotencji; dead-letter zostaje.
* [Obserwowalność i health checki](observability.md) - OpenTelemetry (ślady przez outbox i Kafkę, metryki, logi) włączane adresem OTLP oraz /health/live i /health/ready z limitami czasu.
