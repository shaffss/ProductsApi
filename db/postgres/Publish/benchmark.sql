-- ===== 001_products.sql =====
-- Dane do benchmarków: masowe produkty (liczba w generate_series).
-- Id deterministyczne: b0000000-0000-0000-0000-{n hex} - kod benchmarku wyliczy je z numeru n.
-- Rosnące n daje rosnące Id (jak uuidv7), więc wstawianie nie rozrzuca wpisów po indeksie.
-- Co 10. produkt ma jedną zmianę ceny (Version = 2, historia w 002_product_price_history.sql).
-- Uruchamiać w bazie products (psql -d products).
-- Usunięcie danych:
--   DELETE FROM "ProductPriceHistory" h USING "Products" p WHERE p."Id" = h."ProductId" AND p."Name" LIKE 'Bench %';
--   DELETE FROM "Products" WHERE "Name" LIKE 'Bench %';
INSERT INTO "Products" ("Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version")
SELECT ('b0000000-0000-0000-0000-' || lpad(to_hex(n), 12, '0'))::uuid,
       'Bench ' || (ARRAY['Czujnik temperatury', 'Czujnik wilgotności', 'Przekaźnik czasowy',
                          'Sterownik PLC', 'Zasilacz 24 V DC'])[n % 5 + 1] || ' #' || n,
       (n * 7919 % 99900 + 100) / 100.0, -- 1.00 - 999.99
       (ARRAY['PLN', 'EUR', 'USD'])[n % 3 + 1],
       c.created,
       CASE WHEN n % 10 = 0 THEN c.created + interval '1 day' ELSE c.created END,
       CASE WHEN n % 10 = 0 THEN 2 ELSE 1 END
FROM generate_series(1::bigint, 100000) AS n
CROSS JOIN LATERAL (SELECT timestamptz '2025-10-01 00:00:00+00' + n * interval '30 seconds' AS created) AS c
ON CONFLICT ("Id") DO NOTHING;

-- ===== 002_product_price_history.sql =====
-- Dane do benchmarków: historia dla produktów ze zmienioną ceną (Version = 2) - stara cena o 10% wyższa
INSERT INTO "ProductPriceHistory" ("ProductId", "OldPrice", "OldCurrency", "NewPrice", "NewCurrency", "ChangedAtUtc")
SELECT p."Id", round(p."Price" * 1.1, 2), p."Currency", p."Price", p."Currency", p."UpdatedAtUtc"
FROM "Products" p
WHERE p."Name" LIKE 'Bench %'
  AND p."Version" = 2
  AND NOT EXISTS (SELECT 1 FROM "ProductPriceHistory" h WHERE h."ProductId" = p."Id");

-- ===== 003_update_statistics.sql =====
-- Świeże statystyki i mapa widoczności po masowym wstawianiu - bez nich planista może wybierać złe plany,
-- a index-only scan nie zadziała, co zafałszuje pomiary. VACUUM nie może działać wewnątrz transakcji.
VACUUM (ANALYZE) "Products";
VACUUM (ANALYZE) "ProductPriceHistory";

