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
    /// Reads the current state of the circuit identified by <paramref name="key"/> without causing any state transition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="TryAcquireAsync"/>, this call never claims a trial probe: a circuit reported as
    /// <see cref="CircuitState.HalfOpen"/> still has its probe budget intact for a caller that intends to execute.
    /// It is therefore the correct check for pre-emptive routing — for example, skipping a provider whose circuit is
    /// not closed and dispatching to another one instead.
    /// </para>
    /// <para>
    /// The result is advisory and may be stale: circuit state lives in a distributed counter and can change between
    /// this read and a subsequent acquire. A <see cref="CircuitState.Closed"/> result is not a guarantee that the next
    /// <see cref="TryAcquireAsync"/> will be permitted, so callers must still handle a denial at acquire time.
    /// </para>
    /// </remarks>
    /// <param name="key">The identifier key of the target service or endpoint.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The <see cref="CircuitState"/> observed for <paramref name="key"/>.</returns>
    ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default);

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

/// <summary>
/// Strongly-typed wrapper for injecting circuit breakers scoped to a specific marker policy.
/// </summary>
/// <typeparam name="TPolicy">The marker type representing the policy category.</typeparam>
public interface ICircuitBreaker<TPolicy> : ICircuitBreaker where TPolicy : notnull { }