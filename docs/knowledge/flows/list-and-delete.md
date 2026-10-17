---
type: Flow
title: Lista, usuwanie i grid produktów
description: "GET /api/products ze stronicowaniem skip/take, DELETE /api/products/{id} w jednej transakcji z czyszczeniem cache oraz statyczny frontend (grid) pod /."
resource: ../../../src/Products.Api/wwwroot/app.js
tags: [api, frontend, paging]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-16T09:45:00Z }
---

# Lista

1. **`GET /api/products?skip=0&take=20`:**
   - `skip` ≥ 0 (domyślnie 0), `take` 1-100 (domyślnie 20); błędne wartości dają `400` z `errors.skip` / `errors.take`;
   - odpowiedź: `items`, `total` (liczba wszystkich produktów), `skip`, `take`.
2. **Kolejność:** od najnowszych - `CreatedAtUtc DESC, Id DESC`. `Id` rozstrzyga równe daty, więc kolejność stron jest jednoznaczna.
3. **Baza:**
   - indeks `IX_Products_CreatedAtUtc_Id` (migracja `ProductListIndex`) czytany wstecz;
   - strona przez `OFFSET`;
   - liczba wszystkich przez `COUNT`. Dapper wysyła oba zapytania jednym poleceniem, EF dwoma.
4. **Bez cache:** lista zawsze czyta z bazy. Zmiany cen i usunięcia są widoczne od razu, bez unieważniania stron.
5. **Stronicowanie offset (`skip`/`take`)** wybrał user, bo pozwala skoczyć do dowolnej strony. Koszt i alternatywa:
   - dalekie strony są wolniejsze, bo baza pomija `skip` wierszy - na 100 tys. produktów to kilkadziesiąt milisekund;
   - dopisane w międzyczasie produkty przesuwają strony;
   - stronicowanie po kluczu (keyset) nie ma tych wad, ale daje tylko „następna/poprzednia”.

# Usuwanie

1. **`DELETE /api/products/{id}`:** `204`, `404` gdy produktu nie ma, `400` gdy id nie jest GUID.
2. **Jedna transakcja:** klucze idempotencji, historia cen i produkt. Klucze obce są bez kaskady, więc najpierw wiersze zależne:
   - EF: trzy `ExecuteDelete`;
   - Dapper SQL Server: trzy `DELETE` w transakcji;
   - Dapper PostgreSQL: jedno polecenie z CTE - klucze obce sprawdzane są na końcu polecenia, gdy wiersze zależne są już usunięte.
3. **Po zatwierdzeniu** usunięcie `product:{id}` z Redisa, jak przy [zmianie ceny](change-price.md). Kolejny odczyt daje `404` z bazy, a nie produkt z cache.
4. **Zdarzenia usunięcia nie ma.** Niewysłane zdarzenia zmian ceny tego produktu zostają w [outboxie](../mechanisms/outbox.md) i zostaną wysłane.

# Grid (frontend)

- **Statyczna strona** w `src/Products.Api/wwwroot` (`index.html`, `app.css`, `app.js` jako moduł ES, bez frameworka i budowania), serwowana przez API pod `/` (`UseDefaultFiles` + `UseStaticFiles`). Ten sam origin co API, więc bez CORS. Działa w każdym wariancie i w kontenerze.
- **Stronicowanie:**
  - numery stron (pierwsza, ostatnia, sąsiedztwo bieżącej), skok do strony, rozmiar strony 10-100;
  - stan w adresie (`?page=3&size=20`), więc odświeżenie zostaje na tej samej stronie;
  - po zmianie rozmiaru strony zostaje ta sama pozycja listy.
- **Zaznaczenie i pasek akcji:**
  - wiersz zaznacza kliknięcie albo Enter/spacja, a strzałki przenoszą zaznaczenie na sąsiedni wiersz;
  - obok „Dodaj” są przyciski „Edytuj” (ołówek, zmiana ceny) i „Usuń” (kosz), aktywne tylko przy zaznaczonym wierszu;
  - skróty: dwuklik otwiera edycję, klawisz Delete - usuwanie;
  - zaznaczenie zostaje po odświeżeniu, jeśli produkt jest na wczytanej stronie; nowy produkt jest od razu zaznaczony.
- **Dodawanie:** `Idempotency-Key` generowany przy otwarciu okna, więc podwójne kliknięcie albo ponowienie po błędzie sieci nie tworzy drugiego produktu ([Idempotency-Key](../mechanisms/idempotency-key.md)).
- **Zmiana ceny:**
  - okno czyta produkt (`GET`, `ETag`) i zapisuje z `If-Match` ([optymistyczna współbieżność](../mechanisms/optimistic-concurrency.md));
  - `412` oznacza, że ktoś zmienił produkt w międzyczasie - okno pokazuje aktualne dane i komunikat, a zapis trzeba powtórzyć.
- **Błędy:** walidacja z API (ProblemDetails `errors`) pod polami formularza, pozostałe błędy w komunikacie.
- **Motyw:** domyślnie systemowy; przycisk w nagłówku przełącza jasny i ciemny, a wybór zostaje w `localStorage`. Kolory to `light-dark()` zależne od `color-scheme`, a zapisany motyw ustawia skrypt w `<head>` przed pierwszym renderem, bez mignięcia.
- **Bezpieczeństwo:** dane trafiają na stronę przez `textContent` (bez `innerHTML`), więc nazwy produktów nie są interpretowane jako HTML.

Powiązane: [Kontrakt HTTP](../contracts/http-api.md), [Odczyt produktu](read-product.md), [Schemat bazy](../contracts/database-schema.md)
