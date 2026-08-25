namespace Wiaoj.Webhooks;

/// <summary>
/// Categorizes transient, retryable delivery failures in a transport-agnostic manner.
/// </summary>
public enum TransientFailureReason {
    /// <summary>
    /// Unspecified transient failure.
    /// </summary>
    General = 0,

    /// <summary>
    /// The destination target returned a transient server error (e.g. HTTP 5xx or gRPC Unavailable).
    /// </summary>
    ServerUnavailable = 1,

    /// <summary>
    /// The delivery attempt timed out before receiving a response from the destination.
    /// </summary>
    Timeout = 2,

    /// <summary>
    /// A network socket reset, connection drop, or DNS resolution failure occurred.
    /// </summary>
    NetworkGlitch = 3,

    /// <summary>
    /// The delivery was throttled by local or distributed rate limiting quotas.
    /// </summary>
    RateLimitThrottled = 4,

    /// <summary>
    /// The delivery was fast-failed because the destination endpoint's circuit breaker is in OPEN state.
    /// </summary>
    CircuitBreakerOpen = 5
}