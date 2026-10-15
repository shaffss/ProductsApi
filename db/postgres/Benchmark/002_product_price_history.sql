-- Dane do benchmarków: historia dla produktów ze zmienioną ceną (Version = 2) - stara cena o 10% wyższa
INSERT INTO "ProductPriceHistory" ("ProductId", "OldPrice", "OldCurrency", "NewPrice", "NewCurrency", "ChangedAtUtc")
SELECT p."Id", round(p."Price" * 1.1, 2), p."Currency", p."Price", p."Currency", p."UpdatedAtUtc"
FROM "Products" p
WHERE p."Name" LIKE 'Bench %'
  AND p."Version" = 2
  AND NOT EXISTS (SELECT 1 FROM "ProductPriceHistory" h WHERE h."ProductId" = p."Id");
