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
