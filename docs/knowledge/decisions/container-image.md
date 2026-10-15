---
type: Decision
title: Obraz Dockera i przypięte wersje
description: "Runtime aspnet chiseled-extra bez powłoki, użytkownik bez uprawnień, przypięte wersje obrazów i czas na łagodne zamknięcie."
resource: ../../../src/Products.Api/Dockerfile
tags: [docker, security, operations]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Runtime `aspnet:10.0.13-noble-chiseled-extra`:** bez powłoki i menedżera pakietów - mniejszy obraz i mniejsza powierzchnia ataku. Wariant `extra` zawiera ICU, bo `Microsoft.Data.SqlClient` nie działa w trybie `InvariantGlobalization`. Bibliotek Kerberos nie ma, dlatego połączenie z PostgreSQL ma `GSS Encryption Mode=Disable` - [pułapka](../pitfalls/chiseled-gssapi.md).
- **Użytkownik:** `USER $APP_UID`, czyli `app` (1654), nie root.
- **Przypięte wersje** w compose i w Testcontainers - to samo środowisko przy każdym uruchomieniu:

  | Obraz | Wersja |
  |---|---|
  | SDK .NET | `10.0.402-noble` |
  | SQL Server | `2025-CU9-GDR1-ubuntu-24.04` |
  | PostgreSQL | `18.6` |
  | Redis | `8.10.2` |
  | Kafka | `4.3.1` |
  | Aspire Dashboard | `13.6.1` |

- **`stop_grace_period`:**
  - API: 40 s, czyli dłużej niż `ShutdownTimeout` .NET (30 s) - zatwierdzenie offsetów Kafki i dokończenie wysyłki;
  - bazy i Kafka: 30 s na zapis stanu (checkpoint).
- **Dane na wolumenach:** bazy i Kafka; Kafka wymaga do tego `KAFKA_LOG_DIRS` - [pułapka](../pitfalls/kafka-log-dirs.md). Redis celowo bez persystencji - [decyzja](redis-pure-cache.md).
- **`.editorconfig` w obrazie:** build kopiuje go, żeby obowiązywały te same reguły analizatorów co lokalnie - [pułapka](../pitfalls/docker-build-editorconfig.md).
