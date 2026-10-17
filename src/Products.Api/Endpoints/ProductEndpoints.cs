using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Products.Api.Contracts;
using Products.Core;
using Products.Core.Entities;

namespace Products.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products");

        products.MapGet("/", List)
            .WithSummary("List products")
            .WithDescription("Newest first (CreatedAtUtc, then Id). Paging: skip (default 0) and take "
                + $"(default {ProductService.DefaultPageSize}, 1-{ProductService.MaxPageSize}); total is the number of "
                + "all products. Read from the database, without the cache.");

        products.MapPost("/", Create)
            .WithSummary("Create a product")
            .WithDescription("Name is required (max 200 characters). Price must be greater than zero with at most "
                + "2 decimal places. Currency must be an active currency code: PLN, EUR or USD (case-insensitive). "
                + "Optional Idempotency-Key header: a retried request with the same key returns the product created "
                + "the first time (header Idempotent-Replayed: true) instead of creating another one; the same key "
                + "with a different body is rejected with 422. Keys expire after 24 hours.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // id bez ograniczenia {id:guid} w trasie: niepoprawne id to 400 (niepoprawne dane wejściowe), a nie 404
        products.MapGet("/{id}", Get)
            .WithSummary("Get a product")
            .WithDescription("Cache-aside: Redis first (key product:{id}, TTL 5 min), then the database. "
                + "Response header X-Cache: HIT (from Redis) or MISS (from the database). "
                + "ETag header: product version for conditional PUT (If-Match).")
            .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapPut("/{id}/price", ChangePrice)
            .WithSummary("Change product price")
            .WithDescription("Saves the new price and currency, invalidates the cache entry and publishes "
                + "ProductPriceChanged to Kafka (via transactional outbox). If price and currency are unchanged, "
                + "nothing is saved and the response contains 'message'. Optional If-Match header with the ETag "
                + "from GET: the change is applied only to that version, otherwise 412 (optimistic concurrency).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        products.MapDelete("/{id}", Delete)
            .WithSummary("Delete a product")
            .WithDescription("Deletes the product with its price history and idempotency keys (one transaction) "
                + "and removes it from the cache.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Results<Ok<ProductListResponse>, ValidationProblem>> List(
        int? skip, int? take, ProductService service, CancellationToken ct)
    {
        var result = await service.ListAsync(skip, take, ct);
        if (result.Errors is { } errors)
            return TypedResults.ValidationProblem(errors);
        return TypedResults.Ok(ProductListResponse.From(result.Value!));
    }

    private static async Task<Results<Created<ProductResponse>, ValidationProblem, ProblemHttpResult>> Create(
        CreateProductRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ProductService service,
        HttpResponse response,
        CancellationToken ct)
    {
        // wg specyfikacji IETF wartość jest w cudzysłowie ("abc"); bez cudzysłowu też przyjmowana
        var result = await service.CreateAsync(request.Name, request.Price, request.Currency, idempotencyKey?.Trim('"'), ct);
        if (result.Errors is { } errors)
            return TypedResults.ValidationProblem(errors);
        if (result.Value is not var (product, replayed))
            return TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Idempotency key reused",
                detail: "Idempotency-Key was already used with a different request body.");

        if (replayed)
            response.Headers["Idempotent-Replayed"] = "true";
        response.Headers.ETag = ETag(product);
        return TypedResults.Created($"/api/products/{product.Id}", ProductResponse.From(product));
    }

    // X-Cache: HIT (z Redisa, bez zapytania do bazy) / MISS - do weryfikacji cache-aside
    private static async Task<Results<Ok<ProductResponse>, ValidationProblem, ProblemHttpResult>> Get(
        string id, ProductService service, HttpResponse response, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var productId))
            return InvalidId();

        var (product, fromCache) = await service.GetAsync(productId, ct);
        if (product is null)
            return ProductNotFound(productId);

        response.Headers["X-Cache"] = fromCache ? "HIT" : "MISS";
        response.Headers.ETag = ETag(product);
        return TypedResults.Ok(ProductResponse.From(product));
    }

    private static async Task<Results<Ok<ProductResponse>, ValidationProblem, ProblemHttpResult>> ChangePrice(
        string id,
        ChangePriceRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ProductService service,
        HttpResponse response,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var productId))
            return InvalidId();
        if (!TryParseIfMatch(ifMatch, out var expectedVersion))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["If-Match"] = ["If-Match must be a single ETag returned by GET, e.g. \"3\"."],
            });

        var result = await service.ChangePriceAsync(productId, request.Price, request.Currency, expectedVersion, ct);
        if (result.Errors is { } errors)
            return TypedResults.ValidationProblem(errors);
        if (result.Value is not { } update)
            return ProductNotFound(productId);

        response.Headers.ETag = ETag(update.Product);
        return update switch
        {
            { VersionConflict: true } => TypedResults.Problem(
                statusCode: StatusCodes.Status412PreconditionFailed,
                title: "Product was modified",
                detail: $"Product '{productId}' has version {update.Product.Version}, If-Match expected {expectedVersion}."),
            { Changed: true } => TypedResults.Ok(ProductResponse.From(update.Product)),
            _ => TypedResults.Ok(ProductResponse.From(update.Product) with
            {
                Message = "Price and currency are unchanged - nothing was updated.",
            }),
        };
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> Delete(
        string id, ProductService service, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var productId))
            return InvalidId();
        if (!await service.DeleteAsync(productId, ct))
            return ProductNotFound(productId);
        return TypedResults.NoContent();
    }

    // ETag = wersja produktu (zwiększana przy każdej zmianie) - do warunkowej zmiany ceny (If-Match)
    private static string ETag(Product product) => $"\"{product.Version}\"";

    // If-Match: "3" - zmiana tylko przy tej wersji; brak nagłówka lub * - bez warunku
    private static bool TryParseIfMatch(string? ifMatch, out int? expectedVersion)
    {
        expectedVersion = null;
        if (ifMatch is null or "*")
            return true;
        if (ifMatch is not ['"', .. var version, '"']
            || !int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            return false;
        expectedVersion = parsed;
        return true;
    }

    private static ValidationProblem InvalidId() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["id"] = ["Product id must be a GUID."] });

    private static ProblemHttpResult ProductNotFound(Guid id) => TypedResults.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Product not found",
        detail: $"Product '{id}' does not exist.");
}
