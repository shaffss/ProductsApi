---
type: Event
title: ProductPriceChanged.v1
description: "Zdarzenie zmiany ceny w topicu product-price-changed: pola, klucz, nagłówki, gwarancje dostarczenia i wersjonowanie."
resource: ../../../src/Products.Core/Events/ProductPriceChanged.cs
tags: [kafka, event, contract]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

# Schema

Topic `product-price-changed` (3 partycje). Wartość to JSON (camelCase), klucz to `productId`.

| Pole | Typ | Znaczenie |
|---|---|---|
| `eventId` | uuid (v7) | identyfikator zdarzenia - po nim konsument rozpoznaje duplikaty |
| `productId` | uuid | produkt; także klucz wiadomości |
| `oldPrice` | decimal | cena przed zmianą |
| `newPrice` | decimal | cena po zmianie |
| `currency` | string (ISO 4217) | waluta nowej ceny |
| `oldCurrency` | string (ISO 4217) | waluta poprzedniej ceny |
| `changedAtUtc` | date-time (UTC) | czas zmiany nadany przez bazę |
| `version` | int | wersja produktu po zmianie - kolejność zdarzeń jednego produktu |

| Nagłówek | Wartość |
|---|---|
| `event-type` | `ProductPriceChanged.v1` |
| `traceparent` | W3C trace context - ślad od żądania HTTP |

# Examples

```json
{"eventId":"01a14105-169a-7236-87dd-5f47c7dbfea0","productId":"01a14105-10ec-74ef-ba6c-dcdeac08d385","oldPrice":149.99,"newPrice":129.99,"currency":"PLN","oldCurrency":"PLN","changedAtUtc":"2026-10-15T19:23:54.242368Z","version":2}
```

# Gwarancje

- **At-least-once:** duplikaty są możliwe, więc trzeba je rozpoznawać po `eventId` - [Inbox](../mechanisms/inbox.md).
- **Kolejność w obrębie produktu:** klucz `productId` kieruje zdarzenia produktu do jednej partycji, a [outbox](../mechanisms/outbox.md) wysyła w kolejności zapisu. Między produktami kolejność nie jest gwarantowana.
- **Zdarzenie powstaje tylko przy faktycznej zmianie** ceny albo waluty.

# Wersjonowanie

- Zmiana zgodna wstecz (nowe pole opcjonalne) zostaje w `v1`.
- Zmiana niezgodna wymaga nowego typu, np. `ProductPriceChanged.v2` w nagłówku `event-type`.
- Konsument pomija nieznane typy przed deserializacją, a brak nagłówka traktuje jak `v1` (wiadomości sprzed wersjonowania).

Powiązane: [Zmiana ceny](../flows/change-price.md)
