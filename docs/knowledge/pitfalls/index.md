# SQL Server

* [sqlcmd i QUOTED_IDENTIFIER](sqlcmd-quoted-identifier.md) - sqlcmd domyślnie wyłącza QUOTED_IDENTIFIER, a indeksy filtrowane wymagają go przy tworzeniu i przy każdym zapisie do tabeli.
* [OUTPUT INTO do tabeli z kluczem obcym](output-into-table-with-fk.md) - OUTPUT ... INTO w SQL Server nie może pisać do tabeli z kluczem obcym - potrzebna zmienna tabelaryczna.
* [datetime2 bez strefy czasowej](datetime2-utc-kind.md) - datetime2 z SQL Server wraca jako DateTimeKind.Unspecified (JSON bez Z) - daty trzeba oznaczać jako UTC przy odczycie.

# Narzędzia i build

* [dotnet ef ze starym buildem](ef-tools-stale-build.md) - dotnet ef z --no-build po migrations add używa starego builda bez nowej migracji - baza powstaje pusta.
* [Build obrazu bez .editorconfig](docker-build-editorconfig.md) - Budowanie obrazu bez .editorconfig nie stosuje wyjątków analizatorów - CA1861 w migracjach przerywa dotnet publish.

# Docker i pierwsze uruchomienie

* [Równoległe tworzenie bazy przy pierwszym starcie](concurrent-database-creation.md) - Dwie instancje API z migracjami przy starcie na pustym serwerze tworzą bazę naraz - jedna pada z błędem 1801, bo blokada migracji EF działa dopiero w istniejącej bazie.
* [Npgsql i Kerberos w obrazie chiseled](chiseled-gssapi.md) - W obrazie chiseled nie ma libgssapi_krb5, więc Npgsql przy próbie szyfrowania GSS wypisuje błąd do logu - wyłącza się to w connection stringu.
* [Kafka zapisuje dane poza wolumenem](kafka-log-dirs.md) - Obraz apache/kafka bez KAFKA_LOG_DIRS zapisuje dane w /tmp/kafka-logs w kontenerze - wolumen zostaje pusty, a topic i offsety giną przy odtworzeniu kontenera.
* [docker compose down pomija profile](compose-down-profiles.md) - Samo docker compose down nie usuwa kontenerów z profili, których polecenie nie wskazuje - zostają razem z siecią. Sprzątanie: docker compose --profile "*" down.
* [Docker Desktop zawiesza start kontenera z plikiem z Windows](docker-desktop-bind-mount.md) - Start kontenera z plikiem montowanym z dysku Windows potrafi zawisnąć, a potem silnik nie startuje żadnego nowego kontenera. Compose nie montuje więc plików, a launcher nie tworzy zbędnych kontenerów i ma limit czasu.

# Działanie w tle

* [Wyjątki w BackgroundService i konsumencie Kafki](background-service-exceptions.md) - Nieobsłużony wyjątek w BackgroundService zatrzymuje całe API, a StoreOffset po utracie partycji rzuca wyjątek.
