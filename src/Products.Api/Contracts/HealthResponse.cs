using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Products.Api.Contracts;

// Stan aplikacji i każdej zależności (baza, Redis, Kafka); Variant - działający wariant dostępu do danych (ORM + baza)
public sealed record HealthResponse(
    string Status, string Variant, double DurationMs, Dictionary<string, HealthCheckResponse> Checks)
{
    // Treść błędu tylko na żądanie (Development) - komunikaty wyjątków mogą ujawniać szczegóły infrastruktury
    public static HealthResponse From(HealthReport report, string variant, bool includeErrors) => new(
        report.Status.ToString(),
        variant,
        Math.Round(report.TotalDuration.TotalMilliseconds, 1),
        report.Entries.ToDictionary(e => e.Key, e => new HealthCheckResponse(
            e.Value.Status.ToString(),
            Math.Round(e.Value.Duration.TotalMilliseconds, 1),
            includeErrors ? e.Value.Exception?.Message : null)));
}

public sealed record HealthCheckResponse(
    string Status,
    double DurationMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error);
