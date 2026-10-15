using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Products.Core;
using Products.Core.Abstractions;
using Products.Core.Entities;
using Products.Core.Events;

namespace Products.Infrastructure.Messaging;

// Wysyła wiadomości z outboxa do Kafki. Paczki pod blokadą bazodanową - przy wielu instancjach aplikacji
// wysyła jedna naraz, w kolejności zapisu. Gwarancja at-least-once: duplikaty odfiltruje konsument.
internal sealed partial class OutboxDispatcher(
    IOutboxStore outbox,
    IProducer<string, string> producer,
    IAdminClient admin,
    KafkaOptions options,
    TimeProvider time,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureTopicAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int? published;
            try
            {
                published = await outbox.ProcessBatchAsync(
                    options.OutboxBatchSize, options.OutboxMaxAttempts, PublishAsync, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogDispatchFailed(logger, e);
                published = null;
            }

            // pełna paczka - zaległości, od razu kolejna; inaczej czekamy na nowe wiadomości
            if (published != options.OutboxBatchSize)
                await Task.Delay(options.OutboxPollInterval, time, stoppingToken);
        }
    }

    // Cała paczka wysyłana naraz (producent zachowuje kolejność w partycji), potwierdzenia sprawdzane po kolei.
    // Wynik: liczba potwierdzonych od początku paczki; kolejne (nawet dostarczone) pójdą ponownie.
    private async Task<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> batch, CancellationToken ct)
    {
        var deliveries = new Task[batch.Count];
        for (var i = 0; i < batch.Count; i++)
            deliveries[i] = ProduceAsync(batch[i], ct);

        try
        {
            await Task.WhenAll(deliveries);
            ProductsTelemetry.OutboxPublished.Add(batch.Count);
            return new(batch.Count, null);
        }
        catch
        {
            var failed = Array.FindIndex(deliveries, d => !d.IsCompletedSuccessfully);
            var message = batch[failed];
            var exception = deliveries[failed].Exception?.InnerException;
            var error = exception?.Message ?? "Delivery cancelled";
            var permanent = IsPermanent(exception);

            ProductsTelemetry.OutboxPublished.Add(failed);
            ProductsTelemetry.OutboxFailed.Add(1, new KeyValuePair<string, object?>("error.type", permanent ? "permanent" : "transient"));
            if (permanent && message.Attempts + 1 >= options.OutboxMaxAttempts)
            {
                ProductsTelemetry.OutboxDeadLettered.Add(1);
                LogDeadLettered(logger, message.Id, message.Attempts + 1, error);
            }
            else
                LogNotDelivered(logger, message.Id, error);
            return new(failed, error, permanent);
        }
    }

    // Span wysyłki w śladzie żądania, które zapisało zdarzenie (TraceParent z outboxa); jego kontekst idzie dalej
    // w nagłówku traceparent - obsługa u konsumenta trafia do tego samego śladu
    private async Task ProduceAsync(OutboxMessage message, CancellationToken ct)
    {
        ActivityContext.TryParse(message.TraceParent, null, isRemote: true, out var parent);
        using var activity = ProductsTelemetry.Source.StartActivity($"{message.Topic} publish", ActivityKind.Producer, parent);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", message.Topic);
        activity?.SetTag("messaging.kafka.message.key", message.MessageKey);

        var headers = new Headers { { KafkaHeaders.EventType, Encoding.UTF8.GetBytes(message.Type) } };
        if ((activity?.Id ?? message.TraceParent) is { } traceParent)
            headers.Add(KafkaHeaders.TraceParent, Encoding.UTF8.GetBytes(traceParent));

        try
        {
            await producer.ProduceAsync(
                message.Topic, new Message<string, string> { Key = message.MessageKey, Value = message.Payload, Headers = headers }, ct);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            throw;
        }
        ProductsTelemetry.OutboxDelay.Record((time.GetUtcNow().UtcDateTime - message.OccurredAtUtc).TotalSeconds);
    }

    // Błąd samej wiadomości - ponowienie nic nie zmieni, zużywa próbę. Brak brokera, timeout itp. librdkafka ponawia
    // sam do MessageTimeoutMs, a potem zgłasza Local_MsgTimedOut - te (i błędy konfiguracji) prób nie zużywają.
    private static bool IsPermanent(Exception? e) => e is ProduceException<string, string>
    {
        Error.Code: ErrorCode.MsgSizeTooLarge or ErrorCode.InvalidMsgSize or ErrorCode.RecordListTooLarge
            or ErrorCode.InvalidMsg or ErrorCode.InvalidRecord or ErrorCode.InvalidTimestamp,
    };

    // Topic z zadaną liczbą partycji; bez tego broker utworzyłby go przy pierwszej wysyłce z domyślną (1)
    private async Task EnsureTopicAsync(CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await admin.CreateTopicsAsync(
                    [new TopicSpecification
                    {
                        Name = ProductPriceChanged.Topic,
                        NumPartitions = options.TopicPartitions,
                        ReplicationFactor = options.TopicReplicationFactor,
                    }],
                    new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                LogTopicCreated(logger, ProductPriceChanged.Topic);
                return;
            }
            catch (CreateTopicsException e) when (e.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                return;
            }
            catch (KafkaException e)
            {
                LogTopicNotVerified(logger, ProductPriceChanged.Topic, RetryDelay, e.Message);
                await Task.Delay(RetryDelay, time, ct);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch failed")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} not delivered: {Error}")]
    private static partial void LogNotDelivered(ILogger logger, long messageId, string error);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox message {MessageId} moved to dead letter after {Attempts} attempts: {Error}")]
    private static partial void LogDeadLettered(ILogger logger, long messageId, int attempts, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kafka topic {Topic} created")]
    private static partial void LogTopicCreated(ILogger logger, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kafka unavailable, topic {Topic} not verified - retry in {Delay}: {Error}")]
    private static partial void LogTopicNotVerified(ILogger logger, string topic, TimeSpan delay, string error);
}
