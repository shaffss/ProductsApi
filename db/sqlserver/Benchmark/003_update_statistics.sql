USE [Products];
GO

-- Świeże statystyki po masowym wstawianiu - bez nich optymalizator może wybierać złe plany i zafałszować pomiary
UPDATE STATISTICS dbo.Products WITH FULLSCAN;
UPDATE STATISTICS dbo.ProductPriceHistory WITH FULLSCAN;
GO
