using System.Diagnostics;

namespace Wiaoj.Resilience;

/// <summary>
/// Represents the evaluation outcome of a circuit breaker execution check.
/// </summary>
[DebuggerDisplay("Allowed={IsAllowed}, State={State}, RetryAfter={RetryAfter}")]
public readonly record struct CircuitExecutionDecision {
    /// <summary>Gets a value indicating whether execution is currently permitted.</summary>
    public bool IsAllowed { get; }

    /// <summary>Gets the operational state of the circuit when evaluated.</summary>
    public CircuitState State { get; }

    /// <summary>Gets the remaining duration before execution can be re-evaluated, if the circuit is open.</summary>
    public TimeSpan? RetryAfter { get; }

    private CircuitExecutionDecision(bool isAllowed, CircuitState state, TimeSpan? retryAfter) {
        this.IsAllowed = isAllowed;
        this.State = state;
        this.RetryAfter = retryAfter;
    }

    /// <summary>Creates a decision permitting execution in closed state.</summary>
    public static CircuitExecutionDecision Allowed() {
        return new(true, CircuitState.Closed, null);
    }

    /// <summary>Creates a decision permitting a trial probe execution in half-open state.</summary>
    public static CircuitExecutionDecision HalfOpenProbe() {
        return new(true, CircuitState.HalfOpen, null);
    }

    /// <summary>Creates a decision rejecting execution because the circuit is open.</summary>
    /// <param name="retryAfter">The remaining duration until the break period expires.</param>
    public static CircuitExecutionDecision Denied(TimeSpan retryAfter) {
        Preca.ThrowIfNegative(retryAfter);
        return new(false, CircuitState.Open, retryAfter);
    }
}