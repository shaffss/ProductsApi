using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Products.Core;
using Products.Core.Abstractions;
using Products.Core.Events;

namespace Products.Infrastructure.Messaging;

// Konsument ProductPriceChanged: idempotentnie (ProcessedEvents) unieważnia cache produktu i zapisuje zmianę w logu.
// Offset zapisywany dopiero po obsłużeniu (at-least-once). Nieudana obsługa ponawiana z rosnącym odstępem,
// po ConsumerMaxAttempts zdarzenie jest pomijane (log Error) - jedna wiadomość nie blokuje partycji.
internal sealed partial class PriceChangedConsumer(
    IProcessedEventStore processedEvents,
    IProductCache cache,
    KafkaOptions options,
    TimeProvider time,
    ILogger<PriceChangedConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    // Consume() blokuje wątek - pętla poza wątkiem startu hosta
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    // Nieobsłużony wyjątek w BackgroundService zatrzymałby całe API - konsument jest wtedy tworzony od nowa,
    // a niezatwierdzone wiadomości wracają (idempotentny konsument pominie już obsłużone)
    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogConsumerFailed(logger, e, RestartDelay);
                await Task.Delay(RestartDelay, time, ct);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoOffsetStore = false, // offset do zatwierdzenia dopiero po obsłużeniu wiadomości
        };
        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetLogHandler((_, message) => KafkaLog.Write(logger, message))
            .Build();
        consumer.Subscribe(ProductPriceChanged.Topic);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try
                {
                    result = consumer.Consume(ct);
                }
                catch (ConsumeException e) // np. topic jeszcze nie istnieje
                {
                    LogConsumeFailed(logger, e.Error.Reason);
                    await Task.Delay(RestartDelay, time, ct);
                    continue;
                }

                await HandleAsync(result, ct);
                try
                {
                    consumer.StoreOffset(result);
                }
                catch (KafkaException e)
                {
                    // partycję odebrano w trakcie obsługi (rebalance) - wiadomość wróci, a idempotentny konsument ją pominie
                    LogOffsetNotStored(logger, result.TopicPartitionOffset, e.Error.Reason);
                }
            }
        }
        finally
        {
            consumer.Close(); // zatwierdza offsety i opuszcza grupę
        }
    }

    private async Task HandleAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var headers = result.Message.Headers;

        // ślad żądania zmiany ceny (nagłówek traceparent) - obsługa zdarzenia jako jego część
        var parent = headers?.TryGetLastBytes(KafkaHeaders.TraceParent, out var traceParent) == true
            && ActivityContext.TryParse(Encoding.UTF8.GetString(traceParent), null, isRemote: true, out var context)
                ? context
                : default;
        using var activity = ProductsTelemetry.Source.StartActivity($"{result.Topic} process", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.consumer.group.name", options.ConsumerGroup);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);

        // typ i wersja schematu z nagłówka - nieznane pomijane przed deserializacją; brak nagłówka: wiadomość sprzed wersjonowania
        var type = headers?.TryGetLastBytes(KafkaHeaders.EventType, out var typeBytes) == true
            ? Encoding.UTF8.GetString(typeBytes)
            : ProductPriceChanged.EventType;
        if (type != ProductPriceChanged.EventType)
        {
            LogUnsupportedType(logger, type, result.TopicPartitionOffset);
            Count("skipped");
            return;
        }

        if (Parse(result.Message.Value) is not { } priceChanged)
        {
            LogInvalidMessage(logger, result.TopicPartitionOffset);
            Count("skipped");
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var handled = await processedEvents.TryProcessAsync(
                    options.ConsumerGroup, priceChanged.EventId, _ => ApplyAsync(priceChanged), ct);
                if (!handled)
                    LogDuplicate(logger, priceChanged.EventId);
                Count(handled ? "handled" : "duplicate");
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException && attempt < options.ConsumerMaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(1 << (attempt - 1), 30)); // 1 s, 2 s, 4 s, ...
                LogHandlingRetry(logger, e, priceChanged.EventId, attempt, delay);
                await Task.Delay(delay, time, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // wpis cache wygaśnie najpóźniej po TTL; treść zdarzenia w logu - do ręcznej obsługi
                LogHandlingFailed(logger, e, priceChanged.EventId, attempt, result.Message.Value);
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                Count("failed");
                return;
            }
        }
    }

    private async Task ApplyAsync(ProductPriceChanged e)
    {
        // drugie unieważnienie (pierwsze w PUT) - ponawiane, gdy Redis był niedostępny w chwili zmiany ceny
        if (!await cache.TryRemoveAsync(e.ProductId))
            throw new InvalidOperationException($"Cache invalidation failed for product {e.ProductId}");

        if (e.OldCurrency == e.Currency)
            LogPriceChanged(logger, e.ProductId, e.OldPrice, e.NewPrice, e.Currency);
        else
            LogPriceAndCurrencyChanged(logger, e.ProductId, e.OldPrice, e.NewPrice, e.Currency, e.OldCurrency);
    }

    private static ProductPriceChanged? Parse(string? json)
    {
        try
        {
            return json is null ? null : ProductPriceChanged.FromJson(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Count(string outcome) =>
        ProductsTelemetry.EventsConsumed.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Product price changed: productId={ProductId}, oldPrice={OldPrice}, newPrice={NewPrice}, currency={Currency}")]
    private static partial void LogPriceChanged(ILogger logger, Guid productId, decimal oldPrice, decimal newPrice, string currency);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Product price changed: productId={ProductId}, oldPrice={OldPrice}, newPrice={NewPrice}, currency={Currency}, oldCurrency={OldCurrency}")]
    private static partial void LogPriceAndCurrencyChanged(
        ILogger logger, Guid productId, decimal oldPrice, decimal newPrice, string currency, string oldCurrency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Event {EventId} already processed - skipped")]
    private static partial void LogDuplicate(ILogger logger, Guid eventId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling event {EventId} failed (attempt {Attempt}) - retry in {Delay}")]
    private static partial void LogHandlingRetry(ILogger logger, Exception exception, Guid eventId, int attempt, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handling event {EventId} failed after {Attempts} attempts - skipped: {Payload}")]
    private static partial void LogHandlingFailed(ILogger logger, Exception exception, Guid eventId, int attempts, string payload);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unsupported event type {EventType} skipped at {Offset}")]
    private static partial void LogUnsupportedType(ILogger logger, string eventType, TopicPartitionOffset offset);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid message skipped at {Offset}")]
    private static partial void LogInvalidMessage(ILogger logger, TopicPartitionOffset offset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kafka consume failed: {Error}")]
    private static partial void LogConsumeFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Offset {Offset} not stored: {Error}")]
    private static partial void LogOffsetNotStored(ILogger logger, TopicPartitionOffset offset, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kafka consumer failed - restart in {Delay}")]
    private static partial void LogConsumerFailed(ILogger logger, Exception exception, TimeSpan delay);
}
