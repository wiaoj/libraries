using Microsoft.Extensions.Logging;

namespace Wiaoj.DistributedCounter.Internal.Logging; 
internal static partial class LogExtensions {
    [LoggerMessage(
       EventId = 101,
       Level = LogLevel.Debug,
       Message = "Batch flush completed. Items: {Count}, Duration: {DurationMs}ms")]
    public static partial void LogBatchFlushCompleted(this ILogger logger, int count, double durationMs);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Warning, // Warning yaptım çünkü drift olması inceleme gerektirebilir
        Message = "Self-Healing triggered! Key: {Key}. Expected: {Expected}, Actual from Redis: {Actual}. Drift: {Drift}")]
    public static partial void LogSelfHealing(this ILogger logger, string key, long expected, long actual, long drift);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Error,
        Message = "Redis synchronization failed. Rolling back {Count} counters to local buffer.")]
    public static partial void LogFlushFailed(this ILogger logger, int count, Exception ex);
}