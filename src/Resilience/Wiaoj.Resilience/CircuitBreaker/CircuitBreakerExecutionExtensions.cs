using Wiaoj.Preconditions;

namespace Wiaoj.Resilience;

/// <summary>
/// Extension methods for executing asynchronous delegates under circuit breaker protection.
/// </summary>
public static class CircuitBreakerExecutionExtensions {
    /// <summary>
    /// Executes a delegate under circuit breaker protection, automatically recording success or failure outcomes.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="circuitBreaker">The circuit breaker instance.</param>
    /// <param name="key">The target service key.</param>
    /// <param name="operation">The asynchronous operation delegate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The result produced by the operation.</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is open.</exception>
    public static async ValueTask<TResult> ExecuteAsync<TResult>(
        this ICircuitBreaker circuitBreaker,
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(circuitBreaker);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);

        cancellationToken.ThrowIfCancellationRequested();

        CircuitExecutionDecision decision = await circuitBreaker.TryAcquireAsync(key, cancellationToken).ConfigureAwait(false);

        if(!decision.IsAllowed) {
            throw new CircuitBreakerOpenException(key, decision.RetryAfter);
        }

        try {
            TResult result = await operation(cancellationToken).ConfigureAwait(false);
            await circuitBreaker.OnSuccessAsync(key, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch {
            await circuitBreaker.OnFailureAsync(key, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Executes a non-returning delegate under circuit breaker protection.
    /// </summary>
    /// <param name="circuitBreaker">The circuit breaker instance.</param>
    /// <param name="key">The target service key.</param>
    /// <param name="operation">The asynchronous operation delegate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is open.</exception>
    public static async ValueTask ExecuteAsync(
        this ICircuitBreaker circuitBreaker,
        string key,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(circuitBreaker);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);

        cancellationToken.ThrowIfCancellationRequested();

        CircuitExecutionDecision decision = await circuitBreaker.TryAcquireAsync(key, cancellationToken).ConfigureAwait(false);

        if(!decision.IsAllowed) {
            throw new CircuitBreakerOpenException(key, decision.RetryAfter);
        }

        try {
            await operation(cancellationToken).ConfigureAwait(false);
            await circuitBreaker.OnSuccessAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch {
            await circuitBreaker.OnFailureAsync(key, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}