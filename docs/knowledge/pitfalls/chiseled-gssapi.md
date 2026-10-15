---
type: Pitfall
title: Npgsql i Kerberos w obrazie chiseled
description: "W obrazie chiseled nie ma libgssapi_krb5, więc Npgsql przy próbie szyfrowania GSS wypisuje błąd do logu - wyłącza się to w connection stringu."
resource: ../../../docker-compose.yml
tags: [docker, postgresql, npgsql]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T22:00:00Z }
---

- **Objaw:** w logach wariantów PostgreSQL w kontenerze pojawia się `Error: libgssapi_krb5.so.2: cannot open shared object file: No such file or directory`. Połączenie działa, ale komunikat wygląda jak awaria.
- **Przyczyna:** Npgsql przy łączeniu próbuje negocjować szyfrowanie GSS (Kerberos), a obraz `aspnet:...-noble-chiseled-extra` nie zawiera bibliotek Kerberos. Pełny obraz Debiana je miał, więc problem pojawił się po przejściu na chiseled.
- **Rozwiązanie:** `GSS Encryption Mode=Disable` w connection stringu PostgreSQL w `docker-compose.yml`. Lokalnie na Windows problem nie występuje.

Powiązane: [Obraz Dockera i przypięte wersje](../decisions/container-image.md)
