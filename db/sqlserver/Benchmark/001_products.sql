USE [Products];
GO

-- Dane do benchmarków: masowe produkty (liczba w @ProductCount).
-- Id deterministyczne: B0000000-0000-0000-0000-{n hex} - kod benchmarku wyliczy je z numeru n.
-- Rosnące n daje rosnące Id (jak NEWSEQUENTIALID / EF), więc wstawianie nie fragmentuje indeksu klastrowego.
-- Co 10. produkt ma jedną zmianę ceny (Version = 2, historia w 002_product_price_history.sql).
-- Usunięcie danych:
--   DELETE h FROM dbo.ProductPriceHistory h JOIN dbo.Products p ON p.Id = h.ProductId WHERE p.Name LIKE N'Bench %';
--   DELETE dbo.Products WHERE Name LIKE N'Bench %';
DECLARE @ProductCount int = 100000;

-- Dogenerowanie tylko brakujących numerów (zamiast NOT EXISTS per wiersz): bez złączenia wiersze trafiają
-- do indeksu w kolejności Id. Hash anti-join miesza kolejność i powodował podziały stron (wypełnienie ~58%).
DECLARE @Start int = 1 + (SELECT COUNT(*) FROM dbo.Products WHERE Name LIKE N'Bench %');

INSERT dbo.Products (Id, Name, Price, Currency, CreatedAtUtc, UpdatedAtUtc, Version)
SELECT g.Id, g.Name, g.Price, g.Currency, g.CreatedAtUtc,
       IIF(g.Changed = 1, DATEADD(DAY, 1, g.CreatedAtUtc), g.CreatedAtUtc),
       IIF(g.Changed = 1, 2, 1)
FROM GENERATE_SERIES(@Start, @ProductCount, 1) AS s -- jawny krok: przy @Start > @ProductCount domyślnie liczyłby w dół
CROSS APPLY (SELECT
    CONVERT(uniqueidentifier, 'B0000000-0000-0000-0000-' + CONVERT(char(12), CONVERT(binary(6), s.value), 2)) AS Id,
    CONCAT(N'Bench ', CHOOSE(s.value % 5 + 1, N'Czujnik temperatury', N'Czujnik wilgotności', N'Przekaźnik czasowy',
                                               N'Sterownik PLC', N'Zasilacz 24 V DC'), N' #', s.value) AS Name,
    CAST((CAST(s.value AS bigint) * 7919 % 99900 + 100) / 100.0 AS decimal(18, 2)) AS Price, -- 1.00 - 999.99
    CHOOSE(s.value % 3 + 1, 'PLN', 'EUR', 'USD') AS Currency,
    DATEADD(SECOND, s.value * 30, CAST('2025-10-01' AS datetime2(7))) AS CreatedAtUtc,
    IIF(s.value % 10 = 0, 1, 0) AS Changed
) AS g;
GO
