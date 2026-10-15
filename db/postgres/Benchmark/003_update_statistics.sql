-- Świeże statystyki i mapa widoczności po masowym wstawianiu - bez nich planista może wybierać złe plany,
-- a index-only scan nie zadziała, co zafałszuje pomiary. VACUUM nie może działać wewnątrz transakcji.
VACUUM (ANALYZE) "Products";
VACUUM (ANALYZE) "ProductPriceHistory";
