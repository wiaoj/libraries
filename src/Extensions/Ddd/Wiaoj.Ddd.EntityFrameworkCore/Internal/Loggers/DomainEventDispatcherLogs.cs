using Microsoft.Extensions.Logging;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Loggers;
internal static partial class DomainEventDispatcherLogs {

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Debug,
        Message = "Dispatched {Count} pre-commit domain event(s) within the active transaction.")]
    public static partial void LogDomainEventsProcessed(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Debug,
        Message = "Persisted {Count} outbox message(s) to the change tracker (locked by this instance).")]
    public static partial void LogOutboxMessagesPersisted(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Debug,
        Message = "Published {Count} outbox message(s) to the in-memory fast-path channel after commit.")]
    public static partial void LogOutboxMessagesPublishedToChannel(this ILogger logger, int count);
}
