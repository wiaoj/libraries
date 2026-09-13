namespace Wiaoj.Resilience;

/// <summary>
/// Root configuration options for the resilience engine containing circuit breaker and timeout policy registrations.
/// </summary>
public sealed class ResilienceOptions {
    /// <summary>
    /// Gets the registered circuit breaker policy factories indexed by policy name.
    /// </summary>
    public Dictionary<string, Func<IServiceProvider, ICircuitBreaker>> Policies { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the fallback default circuit breaker factory delegate.
    /// </summary>
    public Func<IServiceProvider, ICircuitBreaker>? DefaultPolicy { get; set; }

    /// <summary>
    /// Gets the registered timeout policy factories indexed by policy name.
    /// </summary>
    public Dictionary<string, Func<IServiceProvider, ITimeoutStrategy>> TimeoutPolicies { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the fallback default timeout strategy factory delegate.
    /// </summary>
    public Func<IServiceProvider, ITimeoutStrategy>? DefaultTimeoutPolicy { get; set; }

    /// <summary>
    /// Gets or sets the options every circuit breaker the factory hands out is wrapped with, so it fails open when its
    /// store fails; <see langword="null"/> hands out the breakers unwrapped.
    /// </summary>
    /// <remarks>Set through <c>FailOpenOnStorageFailure()</c> on the resilience builder.</remarks>
    public ResilientCircuitBreakerOptions? FailOpenOnStorageFailure { get; set; }
}