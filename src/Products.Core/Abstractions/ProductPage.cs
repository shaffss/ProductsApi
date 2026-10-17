using Products.Core.Entities;

namespace Products.Core.Abstractions;

// Strona listy produktów (od najnowszych); Total - liczba wszystkich produktów, do numerów stron
public sealed record ProductPage(IReadOnlyList<Product> Items, long Total, int Skip, int Take);
