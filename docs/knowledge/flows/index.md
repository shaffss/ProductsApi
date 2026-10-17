# Przepływy

* [Utworzenie produktu](create-product.md) - POST /api/products: walidacja, opcjonalny Idempotency-Key, zapis w bazie i odpowiedź 201 z ETag.
* [Odczyt produktu](read-product.md) - GET /api/products/{id}: cache-aside w Redisie, jeden odczyt z bazy przy równoczesnych żądaniach, nagłówki X-Cache i ETag.
* [Zmiana ceny](change-price.md) - PUT /api/products/{id}/price: warunek If-Match, atomowy zapis z outboxem, zdarzenie w Kafce i log konsumenta.
* [Lista i grid produktów](list-and-delete.md) - GET /api/products ze stronicowaniem skip/take i statyczny frontend (grid) pod /; usuwanie produktów wyłączone (DELETE zwraca 405), kod zakomentowany.
