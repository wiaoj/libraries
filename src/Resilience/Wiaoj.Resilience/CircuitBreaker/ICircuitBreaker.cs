namespace Wiaoj.Resilience;

/// <summary>
/// Defines a direction- and transport-agnostic circuit breaker strategy capable of shielding targets from cascading failures.
/// </summary>
public interface ICircuitBreaker {
    /// <summary>
    /// Evaluates whether an operation identified by <paramref name="key"/> is permitted to proceed.
    /// </summary>
    /// <param name="key">The identifier key of the target service or endpoint.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="CircuitExecutionDecision"/> indicating whether execution is allowed.</returns>
    ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful operation outcome, closing the circuit if in trial probe mode and resetting failure metrics.
    /// </summary>
    /// <param name="key">The identifier key of the target service or endpoint.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a transient failure outcome, tripping the circuit if the configured failure criteria are met.
    /// </summary>
    /// <param name="key">The identifier key of the target service or endpoint.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default);
}