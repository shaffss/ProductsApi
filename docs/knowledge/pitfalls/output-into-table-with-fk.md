---
type: Pitfall
title: OUTPUT INTO do tabeli z kluczem obcym
description: "OUTPUT ... INTO w SQL Server nie może pisać do tabeli z kluczem obcym - potrzebna zmienna tabelaryczna."
resource: ../../../src/Products.Persistence/SqlDialect.SqlServer.cs
tags: [sql-server, sql]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Objaw:** `UPDATE dbo.Products ... OUTPUT ... INTO dbo.ProductPriceHistory` kończy się błędem: tabela docelowa `OUTPUT INTO` nie może być po żadnej stronie relacji klucza głównego i obcego.
- **Rozwiązanie:** `OUTPUT ... INTO @changed` (zmienna tabelaryczna), potem `INSERT dbo.ProductPriceHistory ... SELECT ... FROM @changed`. To samo w `InsertProductWithKeySql` (`@product`). W PostgreSQL ten problem nie występuje - zapis idzie przez CTE z `RETURNING`.

Powiązane: [Atomowa zmiana ceny](../mechanisms/atomic-price-change.md)
