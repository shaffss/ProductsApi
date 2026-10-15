USE [Products];
GO

-- Dane demo: historia zmian cen (spójna z bieżącymi cenami z 001_products.sql)
INSERT dbo.ProductPriceHistory (ProductId, OldPrice, OldCurrency, NewPrice, NewCurrency, ChangedAtUtc)
SELECT s.ProductId, s.OldPrice, s.OldCurrency, s.NewPrice, s.NewCurrency, s.ChangedAtUtc
FROM (VALUES
    ('00000000-0000-0000-0000-000000000001',  149.99, 'PLN',  129.99, 'PLN', '2026-09-17T08:00:00'), -- obniżka
    ('00000000-0000-0000-0000-000000000003',  229.00, 'PLN',  249.50, 'PLN', '2026-09-05T10:30:00'), -- podwyżka
    ('00000000-0000-0000-0000-000000000005', 5499.00, 'PLN', 1299.00, 'EUR', '2026-08-10T09:00:00'), -- zmiana waluty
    ('00000000-0000-0000-0000-000000000005', 1299.00, 'EUR', 1249.00, 'EUR', '2026-09-20T12:00:00')
) AS s (ProductId, OldPrice, OldCurrency, NewPrice, NewCurrency, ChangedAtUtc)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductPriceHistory h
                  WHERE h.ProductId = s.ProductId AND h.ChangedAtUtc = s.ChangedAtUtc);
GO
