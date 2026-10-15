using Products.Core.Entities;

namespace Products.Core.Abstractions;

// Wynik odczytu produktu; FromCache - czy odpowiedź pochodzi z cache (bez zapytania do bazy)
public readonly record struct ProductLookup(Product? Product, bool FromCache);
