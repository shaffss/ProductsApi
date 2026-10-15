using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Products.Api.Contracts;
using Products.Persistence;

namespace Products.Api.Endpoints;

// Zwykłe endpointy zamiast MapHealthChecks - dzięki temu są w dokumencie OpenAPI (Swagger) ze schematem odpowiedzi
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("/health").WithTags("Health");

        // gotowość: czy instancja może obsługiwać ruch (np. load balancer, readinessProbe)
        health.MapGet("", GetReadiness)
            .WithSummary("Application health (same as /health/ready)")
            .WithDescription("Database is required (503 when down); Redis and Kafka are optional (Degraded, still 200).")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        health.MapGet("/ready", GetReadiness)
            .WithSummary("Readiness")
            .WithDescription("Checks dependencies: database is required (503 when down); Redis and Kafka are optional.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        // żywotność: sam proces, bez zależności - awaria bazy nie może powodować restartu instancji (livenessProbe)
        health.MapGet("/live", GetLiveness)
            .WithSummary("Liveness")
            .WithDescription("The process responds; dependencies are not checked.")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<JsonHttpResult<HealthResponse>> GetReadiness(
        HealthCheckService health, PersistenceOptions persistence, IHostEnvironment environment, CancellationToken ct)
    {
        var report = await health.CheckHealthAsync(ct);
        return TypedResults.Json(
            HealthResponse.From(report, persistence.Variant, includeErrors: environment.IsDevelopment()),
            statusCode: report.Status == HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK);
    }

    private static async Task<JsonHttpResult<HealthResponse>> GetLiveness(
        HealthCheckService health, PersistenceOptions persistence, CancellationToken ct) =>
        TypedResults.Json(HealthResponse.From(await health.CheckHealthAsync(_ => false, ct), persistence.Variant, includeErrors: false));
}
