-- ===== 001_products.sql =====
USE [Products];
GO

-- Dane demo: produkty (stałe Id - do testów ręcznych i kolekcji Bruno/Postman)
-- Version i UpdatedAtUtc są zgodne z historią cen z 002_product_price_history.sql.
INSERT dbo.Products (Id, Name, Price, Currency, CreatedAtUtc, UpdatedAtUtc, Version)
SELECT s.Id, s.Name, s.Price, s.Currency, s.CreatedAtUtc, s.UpdatedAtUtc, s.Version
FROM (VALUES
    ('00000000-0000-0000-0000-000000000001', N'Czujnik temperatury',          129.99, 'PLN', '2026-09-01T08:00:00', '2026-09-17T08:00:00', 2),
    ('00000000-0000-0000-0000-000000000002', N'Czujnik wilgotności',          189.00, 'PLN', '2026-09-01T08:00:00', '2026-09-01T08:00:00', 1),
    ('00000000-0000-0000-0000-000000000003', N'Czujnik ciśnienia',            249.50, 'PLN', '2026-09-01T08:00:00', '2026-09-05T10:30:00', 2),
    ('00000000-0000-0000-0000-000000000004', N'Przekaźnik czasowy',            89.90, 'PLN', '2026-09-02T09:00:00', '2026-09-02T09:00:00', 1),
    ('00000000-0000-0000-0000-000000000005', N'Sterownik PLC',               1249.00, 'EUR', '2026-08-01T07:00:00', '2026-09-20T12:00:00', 3),
    ('00000000-0000-0000-0000-000000000006', N'Moduł Wi-Fi ESP32',             12.50, 'USD', '2026-09-03T11:00:00', '2026-09-03T11:00:00', 1),
    ('00000000-0000-0000-0000-000000000007', N'Zasilacz 24 V DC',              59.00, 'EUR', '2026-09-03T11:00:00', '2026-09-03T11:00:00', 1),
    ('00000000-0000-0000-0000-000000000008', N'Termostat programowalny',      349.00, 'PLN', '2026-09-04T13:00:00', '2026-09-04T13:00:00', 1),
    ('00000000-0000-0000-0000-000000000009', N'Czujnik ruchu PIR',             39.99, 'PLN', '2026-09-04T13:00:00', '2026-09-04T13:00:00', 1),
    ('00000000-0000-0000-0000-000000000010', N'Licznik energii trójfazowy',   119.00, 'USD', '2026-09-05T14:00:00', '2026-09-05T14:00:00', 1)
) AS s (Id, Name, Price, Currency, CreatedAtUtc, UpdatedAtUtc, Version)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Id = s.Id);
GO

-- ===== 002_product_price_history.sql =====
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

