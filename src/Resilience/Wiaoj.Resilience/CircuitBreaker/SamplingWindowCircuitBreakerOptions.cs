using Wiaoj.Preconditions;

namespace Wiaoj.Resilience;

/// <summary>
/// Configuration options for the percentage-based sampling window circuit breaker strategy.
/// </summary>
public sealed class SamplingWindowCircuitBreakerOptions {
    /// <summary>The default failure rate threshold (0.5 = 50%).</summary>
    public const double DefaultFailureRateThreshold = 0.5;

    /// <summary>The default minimum request volume before evaluating failure percentage (10).</summary>
    public const int DefaultMinimumThroughput = 10;

    /// <summary>The default lookback sampling window duration (30 seconds).</summary>
    public static readonly TimeSpan DefaultSamplingWindow = TimeSpan.FromSeconds(30);

    /// <summary>The default duration the circuit remains open upon tripping (1 minute).</summary>
    public static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromMinutes(1);

    /// <summary>The default maximum number of concurrent probe requests permitted during half-open state (5).</summary>
    public const int DefaultPermittedNumberOfCallsInHalfOpenState = 5;

    /// <summary>Gets or sets the failure rate ratio (between 0.0 and 1.0) required to trip the circuit. Default is 0.5 (50%).</summary>
    public double FailureRateThreshold { get; set; } = DefaultFailureRateThreshold;

    /// <summary>Gets or sets the minimum number of requests required within a sampling window before evaluating the failure rate. Default is 10.</summary>
    public int MinimumThroughput { get; set; } = DefaultMinimumThroughput;

    /// <summary>Gets or sets the rolling lookback window duration across which success and failure ratios are calculated. Default is 30 seconds.</summary>
    public TimeSpan SamplingWindow { get; set; } = DefaultSamplingWindow;

    /// <summary>Gets or sets the duration the circuit remains open before entering half-open probing. Default is 1 minute.</summary>
    public TimeSpan BreakDuration { get; set; } = DefaultBreakDuration;

    /// <summary>Gets or sets the maximum number of concurrent trial probe requests permitted during the half-open recovery state. Default is 5.</summary>
    public int PermittedNumberOfCallsInHalfOpenState { get; set; } = DefaultPermittedNumberOfCallsInHalfOpenState;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any value is out of valid bounds.</exception>
    public void Validate() {
        if(this.FailureRateThreshold is <= 0.0 or > 1.0) {
            throw new ArgumentOutOfRangeException(nameof(this.FailureRateThreshold), "Failure rate threshold must be greater than 0.0 and less than or equal to 1.0.");
        }
        Preca.ThrowIfLessThan(this.MinimumThroughput, 1);
        Preca.ThrowIfLessThan(this.PermittedNumberOfCallsInHalfOpenState, 1);
        if(this.SamplingWindow <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.SamplingWindow), "Sampling window must be greater than zero.");
        }
        if(this.BreakDuration <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.BreakDuration), "Break duration must be greater than zero.");
        }
    }
}