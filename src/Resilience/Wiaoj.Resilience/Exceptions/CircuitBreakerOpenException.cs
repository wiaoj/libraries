namespace Wiaoj.Resilience;

/// <summary>
/// Exception thrown when an execution attempt is blocked because the target circuit breaker is in the <see cref="CircuitState.Open"/> state.
/// </summary>
public sealed class CircuitBreakerOpenException : Exception {
    /// <summary>Gets the identifier key of the circuit that blocked execution.</summary>
    public string Key { get; }

    /// <summary>Gets the remaining duration before trial probe execution is permitted, if known.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
    /// </summary>
    /// <param name="key">The circuit target key.</param>
    /// <param name="retryAfter">The remaining duration until the break period expires.</param>
    public CircuitBreakerOpenException(string key, TimeSpan? retryAfter)
        : base($"Circuit breaker is OPEN for key '{key}'. Execution is blocked for {retryAfter?.TotalMilliseconds ?? 0:F0}ms.") {
        this.Key = key;
        this.RetryAfter = retryAfter;
    }
}