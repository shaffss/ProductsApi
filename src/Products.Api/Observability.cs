using System.Diagnostics;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Products.Core;

namespace Products.Api;

public static class Observability
{
    // OpenTelemetry: ślady, metryki i logi przez OTLP (np. Aspire Dashboard z docker-compose.yml).
    // Tylko z ustawionym OTEL_EXPORTER_OTLP_ENDPOINT - bez niego żadnego narzutu (np. w benchmarkach).
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            return builder;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "products-api"))
            .WithTracing(tracing => tracing
                .AddSource(ProductsTelemetry.Name) // wysyłka i obsługa zdarzeń Kafki
                .AddAspNetCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddNpgsql()
                .SetSampler(new ParentBasedSampler(new SkipRootDatabaseCallsSampler())))
            .WithMetrics(metrics => metrics
                .AddMeter(ProductsTelemetry.Name, "System.Runtime")
                .AddAspNetCoreInstrumentation())
            .WithLogging()
            .UseOtlpExporter();
        return builder;
    }

    // Zapytania do bazy bez nadrzędnego spanu (pętla outboxa co sekundę, retencja, migracje) nie trafiają do śladów -
    // zasłoniłyby żądania. W żądaniach HTTP i obsłudze zdarzeń zapytania są częścią śladu (decyduje rodzic).
    private sealed class SkipRootDatabaseCallsSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters parameters) =>
            new(parameters.Kind == ActivityKind.Client ? SamplingDecision.Drop : SamplingDecision.RecordAndSample);
    }
}
