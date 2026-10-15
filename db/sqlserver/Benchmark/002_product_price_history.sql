USE [Products];
GO

-- Dane do benchmarków: historia dla produktów ze zmienioną ceną (Version = 2) - stara cena o 10% wyższa
INSERT dbo.ProductPriceHistory (ProductId, OldPrice, OldCurrency, NewPrice, NewCurrency, ChangedAtUtc)
SELECT p.Id, ROUND(p.Price * 1.1, 2), p.Currency, p.Price, p.Currency, p.UpdatedAtUtc
FROM dbo.Products p
WHERE p.Name LIKE N'Bench %'
  AND p.Version = 2
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductPriceHistory h WHERE h.ProductId = p.Id);
GO
