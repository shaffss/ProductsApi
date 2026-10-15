using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Products.Infrastructure.Messaging;

internal static partial class KafkaLog
{
    // Logi librdkafka do ILogger (domyślnie trafiają na stderr z pominięciem konfiguracji logowania)
    public static void Write(ILogger logger, LogMessage message)
    {
        var level = (LogLevel)message.LevelAs(LogLevelType.MicrosoftExtensionsLogging);
        Write(logger, level, message.Facility, message.Message);
    }

    [LoggerMessage(Message = "librdkafka {Facility}: {Message}")]
    private static partial void Write(ILogger logger, LogLevel level, string facility, string message);
}
