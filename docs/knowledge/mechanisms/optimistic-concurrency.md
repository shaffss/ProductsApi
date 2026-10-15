---
type: Mechanism
title: Optymistyczna współbieżność (ETag / If-Match)
description: "ETag to wersja produktu; PUT z If-Match zmienia cenę tylko przy tej wersji, inaczej 412."
resource: ../../../src/Products.Api/Endpoints/ProductEndpoints.cs
tags: [api, concurrency, http]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- `Version` rośnie przy każdej zmianie ceny. `ETag: "{Version}"` jest w odpowiedziach POST, GET i PUT, także przy `412` (aktualna wersja).
- `If-Match: "3"`: warunek `Version = 3` jest częścią [atomowego UPDATE](atomic-price-change.md) - bez osobnego odczytu i bez wyścigu.
- Brak zmiany przy innej wersji to `VersionConflict` i odpowiedź `412 Precondition Failed`. Warunek ma pierwszeństwo przed „ta sama cena”, bo RFC 9110 każe najpierw sprawdzić warunki.
- `If-Match: *` albo brak nagłówka - bez warunku.
- Inny format (bez cudzysłowu, słaby `W/"3"`, kilka wartości) daje `400` z błędem pola `If-Match`.

Chroni przed nadpisaniem zmiany innego klienta: klient dostaje `412`, pobiera produkt ponownie i decyduje od nowa.

Powiązane: [Zmiana ceny](../flows/change-price.md), [Kontrakt HTTP](../contracts/http-api.md)
