---
type: Pitfall
title: Build obrazu bez .editorconfig
description: "Budowanie obrazu bez .editorconfig nie stosuje wyjątków analizatorów - CA1861 w migracjach przerywa dotnet publish."
resource: ../../../src/Products.Api/Dockerfile
tags: [docker, analyzers, build]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Objaw:** lokalnie build przechodzi, a `docker build` kończy się błędem CA1861 w plikach migracji.
- **Przyczyna:** Dockerfile kopiuje do etapu build tylko wybrane pliki. Bez `.editorconfig` migracje nie są oznaczone jako `generated_code`, więc analizatory je sprawdzają, a ostrzeżenia są błędami (`TreatWarningsAsErrors`).
- **Rozwiązanie:** `COPY ... .editorconfig ./` razem z `Directory.Build.props` i `Directory.Packages.props`. Krok `docker build` w CI wychwyci regresję.

Powiązane: [Obraz Dockera i przypięte wersje](../decisions/container-image.md)
