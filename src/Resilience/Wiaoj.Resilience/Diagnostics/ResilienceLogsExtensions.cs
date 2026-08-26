using Microsoft.Extensions.Logging;

namespace Wiaoj.Resilience.Diagnostics;

/// <summary>
/// Source-generated logging extensions for circuit breaker operations.
/// </summary>
internal static partial class ResilienceLogsExtensions {
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "[{Strategy}] Circuit breaker TRIPPED to OPEN for key '{Key}'. Reason: {Reason}. Break duration: {DurationMs:F0}ms.")]
    public static partial void LogCircuitTripped(
        this ILogger logger, string strategy, string key, string reason, double durationMs);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "[{Strategy}] Trial probe permitted in Half-Open state for key '{Key}'.")]
    public static partial void LogProbePermitted(
        this ILogger logger, string strategy, string key);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "[{Strategy}] Execution denied; circuit is OPEN for key '{Key}'. RetryAfter: {RetryAfterSeconds:F3}s")]
    public static partial void LogExecutionDenied(
        this ILogger logger, string strategy, string key, double retryAfterSeconds);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "[{Strategy}] Operation succeeded; circuit CLOSED and metrics reset for key '{Key}'.")]
    public static partial void LogCircuitClosed(
        this ILogger logger, string strategy, string key);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "[{Strategy}] Transient failure recorded for key '{Key}'. Current Failure Count / Rate: {Metric:F2}")]
    public static partial void LogTransientFailure(
        this ILogger logger, string strategy, string key, double metric);
}