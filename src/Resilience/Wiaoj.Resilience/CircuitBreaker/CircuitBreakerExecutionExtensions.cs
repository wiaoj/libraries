using System.Runtime.CompilerServices;
using Wiaoj.Preconditions;

namespace Wiaoj.Resilience;

/// <summary>
/// Extension methods for executing asynchronous delegates under circuit breaker protection with optional graceful fallback degradation.
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
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
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

    /// <summary>
    /// Executes a delegate under circuit breaker protection, returning a static fallback value if execution is blocked or fails.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="circuitBreaker">The circuit breaker instance.</param>
    /// <param name="key">The target service key.</param>
    /// <param name="operation">The asynchronous operation delegate.</param>
    /// <param name="fallbackValue">The static value to return if execution fails or the circuit is open.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The result produced by the operation, or <paramref name="fallbackValue"/> if execution failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    public static async ValueTask<TResult> ExecuteWithFallbackAsync<TResult>(
        this ICircuitBreaker circuitBreaker,
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        TResult fallbackValue,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(circuitBreaker);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);

        try {
            return await circuitBreaker.ExecuteAsync(key, operation, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch {
            return fallbackValue;
        }
    }

    /// <summary>
    /// Executes a delegate under circuit breaker protection, invoking an asynchronous fallback factory if execution is blocked or fails.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="circuitBreaker">The circuit breaker instance.</param>
    /// <param name="key">The target service key.</param>
    /// <param name="operation">The asynchronous operation delegate.</param>
    /// <param name="fallbackFactory">The fallback factory delegate receiving the triggering exception.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The result produced by the operation or by the fallback factory.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    [OverloadResolutionPriority(1)]
    public static async ValueTask<TResult> ExecuteWithFallbackAsync<TResult>(
        this ICircuitBreaker circuitBreaker,
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        Func<Exception, CancellationToken, ValueTask<TResult>> fallbackFactory,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(circuitBreaker);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);
        Preca.ThrowIfNull(fallbackFactory);

        try {
            return await circuitBreaker.ExecuteAsync(key, operation, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception ex) {
            return await fallbackFactory(ex, cancellationToken).ConfigureAwait(false);
        }
    }
}