using Wiaoj.Abstractions;

namespace Wiaoj.Resilience;

/// <summary>
/// Configuration options for the consecutive failures circuit breaker strategy.
/// </summary>
public sealed class CircuitBreakerOptions : IDeepCloneable<CircuitBreakerOptions>, IMergeable<CircuitBreakerOptions> {
    /// <summary>The default consecutive failure threshold required to trip the circuit (5).</summary>
    public const int DefaultFailureThreshold = 5;

    /// <summary>The default duration the circuit remains open before entering half-open probing (1 minute).</summary>
    public static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromMinutes(1);

    /// <summary>The default storage key prefix.</summary>
    public const string DefaultKeyPrefix = "wiaoj:resilience:cb:";

    /// <summary>Gets or sets the storage key prefix for isolation. Default is <c>"wiaoj:resilience:cb:"</c>.</summary>
    public string KeyPrefix { get; set; } = DefaultKeyPrefix;

    /// <summary>Gets or sets the number of consecutive failures required to trip the circuit. Default is 5.</summary>
    public int FailureThreshold { get; set; } = DefaultFailureThreshold;

    /// <summary>Gets or sets the duration the circuit remains open before attempting recovery. Default is 1 minute.</summary>
    public TimeSpan BreakDuration { get; set; } = DefaultBreakDuration;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any value is out of valid bounds.</exception>
    public void Validate() {
        Preca.ThrowIfNullOrWhiteSpace(this.KeyPrefix);
        Preca.ThrowIfLessThan(this.FailureThreshold, 1);
        if(this.BreakDuration <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.BreakDuration), "Break duration must be greater than zero.");
        }
    }

    /// <inheritdoc/>
    public CircuitBreakerOptions DeepClone() {
        return new CircuitBreakerOptions {
            KeyPrefix = this.KeyPrefix,
            FailureThreshold = this.FailureThreshold,
            BreakDuration = this.BreakDuration
        };
    }

    /// <inheritdoc/>
    public CircuitBreakerOptions Merge(CircuitBreakerOptions? other) {
        CircuitBreakerOptions clone = DeepClone();
        if(other is null) return clone;

        clone.KeyPrefix = string.IsNullOrWhiteSpace(other.KeyPrefix) ? clone.KeyPrefix : other.KeyPrefix;
        clone.FailureThreshold = other.FailureThreshold;
        clone.BreakDuration = other.BreakDuration;
        return clone;
    }
}