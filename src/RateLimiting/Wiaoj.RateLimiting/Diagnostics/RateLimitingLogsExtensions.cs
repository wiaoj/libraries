using Microsoft.Extensions.Logging;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Source-generated logging extensions for rate limiting operations.
/// </summary>
internal static partial class RateLimitingLogsExtensions {
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Request allowed. Key: '{Key}', Algorithm: {Algorithm}, Cost: {Cost}, Remaining: {Remaining}")]
    public static partial void LogAcquireAllowed(
        this ILogger logger, string key, string algorithm, int cost, long? remaining);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Request denied. Key: '{Key}', Algorithm: {Algorithm}, Cost: {Cost}, RetryAfter: {RetryAfterSeconds:F3}s, Remaining: {Remaining}")]
    public static partial void LogAcquireDenied(
        this ILogger logger, string key, string algorithm, int cost, double? retryAfterSeconds, long? remaining);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Request queued for traffic shaping. Key: '{Key}', Algorithm: {Algorithm}, WaitDuration: {WaitDurationMs:F2}ms, Cost: {Cost}")]
    public static partial void LogQueueSuspended(
        this ILogger logger, string key, string algorithm, double waitDurationMs, int cost);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "Queued request turn arrived and released. Key: '{Key}', Algorithm: {Algorithm}, Waited: {ElapsedWaitMs:F2}ms")]
    public static partial void LogQueueReleased(
        this ILogger logger, string key, string algorithm, double elapsedWaitMs);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Trace,
        Message = "Speculative increment rolled back. Key: '{Key}', Algorithm: {Algorithm}, Cost: {Cost}, Reason: '{Reason}'")]
    public static partial void LogRollbackExecuted(
        this ILogger logger, string key, string algorithm, int cost, string reason);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Warning,
        Message = "Queued request wait cancelled; reservation rolled back. Key: '{Key}', Algorithm: {Algorithm}, Cost: {Cost}")]
    public static partial void LogQueueCancelled(
        this ILogger logger, string key, string algorithm, int cost);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Error,
        Message = "Storage failure occurred while evaluating key '{Key}' on {Algorithm}. Executing Fail-Open fallback (Request Allowed).")]
    public static partial void LogStorageFailureFallback(
        this ILogger logger, string key, string algorithm, Exception exception);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Debug,
        Message = "Request short-circuited by local negative cache. Key: '{Key}', Algorithm: {Algorithm}, RetryAfter: {RetryAfterSeconds:F3}s")]
    public static partial void LogNegativeCacheHit(
        this ILogger logger, string key, string algorithm, double retryAfterSeconds);
}