using Products.Core.Entities;

namespace Products.Core.Abstractions;

// Wynik utworzenia produktu; Replayed - ponowione żądanie z tym samym kluczem idempotencji:
// produkt utworzony za pierwszym razem, nic nowego nie zapisano
public sealed record CreatedProduct(Product Product, bool Replayed);
