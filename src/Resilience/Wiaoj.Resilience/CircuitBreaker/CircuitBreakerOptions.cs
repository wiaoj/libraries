namespace Wiaoj.Resilience;

/// <summary>
/// Configuration options for the consecutive failures circuit breaker strategy.
/// </summary>
public sealed class CircuitBreakerOptions {
    /// <summary>The default consecutive failure threshold required to trip the circuit (5).</summary>
    public const int DefaultFailureThreshold = 5;

    /// <summary>The default duration the circuit remains open before entering half-open probing (1 minute).</summary>
    public static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the number of consecutive failures required to trip the circuit. Default is 5.</summary>
    public int FailureThreshold { get; set; } = DefaultFailureThreshold;

    /// <summary>Gets or sets the duration the circuit remains open before attempting recovery. Default is 1 minute.</summary>
    public TimeSpan BreakDuration { get; set; } = DefaultBreakDuration;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any value is out of valid bounds.</exception>
    public void Validate() {
        Preca.ThrowIfLessThan(this.FailureThreshold, 1);
        if(this.BreakDuration <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.BreakDuration), "Break duration must be greater than zero.");
        }
    }
}