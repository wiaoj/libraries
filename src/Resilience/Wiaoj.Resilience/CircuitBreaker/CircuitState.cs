namespace Wiaoj.Resilience;

/// <summary>
/// Represents the operational state of an endpoint circuit breaker.
/// </summary>
public enum CircuitState {
    /// <summary>The circuit is operational; execution requests proceed normally.</summary>
    Closed = 0,

    /// <summary>The circuit is tripped due to failure thresholds; requests are fast-failed.</summary>
    Open = 1,

    /// <summary>The break duration elapsed; trial probe requests are permitted to test target recovery.</summary>
    HalfOpen = 2
}