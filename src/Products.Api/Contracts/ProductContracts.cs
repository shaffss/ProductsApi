using System.Text.Json.Serialization;
using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Api.Contracts;

// Pola nullable: brak pola w JSON daje błąd walidacji "required", a nie po cichu 0 / null
public sealed record CreateProductRequest(string? Name, decimal? Price, string? Currency);

public sealed record ChangePriceRequest(decimal? Price, string? Currency);

public sealed record ProductResponse(Guid Id, string Name, decimal Price, string Currency, DateTime UpdatedAtUtc)
{
    // tylko PUT /price bez zmian - wyjaśnienie, dlaczego nic nie zapisano; w pozostałych odpowiedziach pominięte
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    // Cena zawsze z 2 miejscami po przecinku, jak decimal(18,2) w bazie (299 -> 299.00): ten sam JSON w każdym
    // wariancie - EF przy utworzeniu zwraca wartość z żądania, Dapper odczytaną z bazy
    public static ProductResponse From(Product p) => new(p.Id, p.Name, p.Price + 0.00m, p.Currency, p.UpdatedAtUtc);
}

// Strona listy od najnowszych; Total - liczba wszystkich produktów (do numerów stron)
public sealed record ProductListResponse(IReadOnlyList<ProductResponse> Items, long Total, int Skip, int Take)
{
    public static ProductListResponse From(ProductPage page) =>
        new([.. page.Items.Select(ProductResponse.From)], page.Total, page.Skip, page.Take);
}
