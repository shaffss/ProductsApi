-- ===== 001_products.sql =====
-- Dane demo: produkty (stałe Id - do testów ręcznych i kolekcji Bruno/Postman)
-- Version i UpdatedAtUtc są zgodne z historią cen z 002_product_price_history.sql.
-- Uruchamiać w bazie products (psql -d products).
INSERT INTO "Products" ("Id", "Name", "Price", "Currency", "CreatedAtUtc", "UpdatedAtUtc", "Version")
VALUES
    ('00000000-0000-0000-0000-000000000001', 'Czujnik temperatury',          129.99, 'PLN', '2026-09-01 08:00:00+00', '2026-09-17 08:00:00+00', 2),
    ('00000000-0000-0000-0000-000000000002', 'Czujnik wilgotności',          189.00, 'PLN', '2026-09-01 08:00:00+00', '2026-09-01 08:00:00+00', 1),
    ('00000000-0000-0000-0000-000000000003', 'Czujnik ciśnienia',            249.50, 'PLN', '2026-09-01 08:00:00+00', '2026-09-05 10:30:00+00', 2),
    ('00000000-0000-0000-0000-000000000004', 'Przekaźnik czasowy',            89.90, 'PLN', '2026-09-02 09:00:00+00', '2026-09-02 09:00:00+00', 1),
    ('00000000-0000-0000-0000-000000000005', 'Sterownik PLC',               1249.00, 'EUR', '2026-08-01 07:00:00+00', '2026-09-20 12:00:00+00', 3),
    ('00000000-0000-0000-0000-000000000006', 'Moduł Wi-Fi ESP32',             12.50, 'USD', '2026-09-03 11:00:00+00', '2026-09-03 11:00:00+00', 1),
    ('00000000-0000-0000-0000-000000000007', 'Zasilacz 24 V DC',              59.00, 'EUR', '2026-09-03 11:00:00+00', '2026-09-03 11:00:00+00', 1),
    ('00000000-0000-0000-0000-000000000008', 'Termostat programowalny',      349.00, 'PLN', '2026-09-04 13:00:00+00', '2026-09-04 13:00:00+00', 1),
    ('00000000-0000-0000-0000-000000000009', 'Czujnik ruchu PIR',             39.99, 'PLN', '2026-09-04 13:00:00+00', '2026-09-04 13:00:00+00', 1),
    ('00000000-0000-0000-0000-000000000010', 'Licznik energii trójfazowy',   119.00, 'USD', '2026-09-05 14:00:00+00', '2026-09-05 14:00:00+00', 1)
ON CONFLICT ("Id") DO NOTHING;

-- ===== 002_product_price_history.sql =====
-- Dane demo: historia zmian cen (spójna z bieżącymi cenami z 001_products.sql)
INSERT INTO "ProductPriceHistory" ("ProductId", "OldPrice", "OldCurrency", "NewPrice", "NewCurrency", "ChangedAtUtc")
SELECT s.*
FROM (VALUES
    ('00000000-0000-0000-0000-000000000001'::uuid,  149.99, 'PLN',  129.99, 'PLN', '2026-09-17 08:00:00+00'::timestamptz), -- obniżka
    ('00000000-0000-0000-0000-000000000003',        229.00, 'PLN',  249.50, 'PLN', '2026-09-05 10:30:00+00'), -- podwyżka
    ('00000000-0000-0000-0000-000000000005',       5499.00, 'PLN', 1299.00, 'EUR', '2026-08-10 09:00:00+00'), -- zmiana waluty
    ('00000000-0000-0000-0000-000000000005',       1299.00, 'EUR', 1249.00, 'EUR', '2026-09-20 12:00:00+00')
) AS s ("ProductId", "OldPrice", "OldCurrency", "NewPrice", "NewCurrency", "ChangedAtUtc")
WHERE NOT EXISTS (SELECT 1 FROM "ProductPriceHistory" h
                  WHERE h."ProductId" = s."ProductId" AND h."ChangedAtUtc" = s."ChangedAtUtc");

