namespace Wiaoj.Resilience.CircuitBreaker;

/// <summary>
/// Contract for tracking, persisting, and evaluating circuit breaker state machines per target key.
/// </summary>
public interface ICircuitBreakerStore {
    /// <summary>Evaluates whether an execution attempt is permitted for the specified target key.</summary>
    ValueTask<CircuitExecutionDecision> CanExecuteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Records a successful execution outcome, resetting consecutive failures and closing the circuit.</summary>
    ValueTask RecordSuccessAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Records a transient failure outcome, tripping the circuit if the failure threshold is reached.</summary>
    ValueTask RecordFailureAsync(string key, CircuitBreakerOptions options, CancellationToken cancellationToken = default);
}