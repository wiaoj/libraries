using System.Diagnostics;
using System.Runtime.CompilerServices;
using Wiaoj.Preconditions;
using Wiaoj.Resilience.Diagnostics;

namespace Wiaoj.Resilience;

/// <summary>
/// Extension methods for executing asynchronous delegates under circuit breaker protection with optional graceful fallback degradation.
/// </summary>
/// <remarks>
/// <para>
/// Only the operation is inside the <c>try</c>. Recording its outcome with the breaker happens after, and can never
/// change what the caller sees: once the operation has run, its result — or its own exception — is what comes back.
/// </para>
/// <para>
/// Recording used to share the operation's <c>try</c>. When the breaker's store was unreachable, a successful
/// operation's <c>OnSuccessAsync</c> threw, the <c>catch</c> recorded a failure, and the caller received an exception
/// for work that had completed. A caller that retries on exception — the normal response — then ran a non-idempotent
/// operation a second time.
/// </para>
/// <para>
/// Acquiring still throws when the store is unreachable: at that point the operation has not run, so nothing is
/// repeated. Wrap the breaker in <see cref="ResilientCircuitBreaker"/> to let calls through instead.
/// </para>
/// </remarks>
public static class CircuitBreakerExecutionExtensions {
    /// <summary>
    /// Executes a delegate under circuit breaker protection, automatically recording success or failure outcomes.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="circuitBreaker">The circuit breaker instance.</param>
    /// <param name="key">The target service key.</param>
    /// <param name="operation">The asynchronous operation delegate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The result produced by the operation, even when its success could not be recorded.</returns>
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

        using Activity? activity = ResilienceTracing.StartExecution(key);
        await AcquireAsync(circuitBreaker, key, activity, cancellationToken).ConfigureAwait(false);

        TResult result;
        try {
            result = await operation(cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            ResilienceTracing.MarkCancelled(activity);
            throw;
        }
        catch(Exception exception) {
            await RecordFailureAsync(circuitBreaker, key, activity, exception, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await RecordSuccessAsync(circuitBreaker, key, activity, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Executes a non-returning delegate under circuit breaker protection.
    /// </summary>
    /// <param name="circuitBreaker">The circuit breaker instance.</param>
    /// <param name="key">The target service key.</param>
    /// <param name="operation">The asynchronous operation delegate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes when the operation has, even when its success could not be recorded.</returns>
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

        using Activity? activity = ResilienceTracing.StartExecution(key);
        await AcquireAsync(circuitBreaker, key, activity, cancellationToken).ConfigureAwait(false);

        try {
            await operation(cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            ResilienceTracing.MarkCancelled(activity);
            throw;
        }
        catch(Exception exception) {
            await RecordFailureAsync(circuitBreaker, key, activity, exception, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await RecordSuccessAsync(circuitBreaker, key, activity, cancellationToken).ConfigureAwait(false);
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
    /// <returns>
    /// The result produced by the operation, or <paramref name="fallbackValue"/> if the operation was not permitted or
    /// failed. An operation that succeeded returns its own result, even when its success could not be recorded.
    /// </returns>
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
    /// <returns>
    /// The result produced by the operation or by the fallback factory. An operation that succeeded returns its own
    /// result, even when its success could not be recorded.
    /// </returns>
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

    private static async ValueTask AcquireAsync(ICircuitBreaker circuitBreaker, string key, Activity? activity, CancellationToken cancellationToken) {
        CircuitExecutionDecision decision = await circuitBreaker.TryAcquireAsync(key, cancellationToken).ConfigureAwait(false);
        ResilienceTracing.RecordDecision(activity, decision);

        if(!decision.IsAllowed) {
            ResilienceTracing.MarkDenied(activity, decision.RetryAfter);
            throw new CircuitBreakerOpenException(key, decision.RetryAfter);
        }
    }

    /// <summary>Records a success; the operation already completed, so a failure to record is traced and not thrown.</summary>
    private static async ValueTask RecordSuccessAsync(ICircuitBreaker circuitBreaker, string key, Activity? activity, CancellationToken cancellationToken) {
        ResilienceTracing.MarkSuccess(activity);

        try {
            await circuitBreaker.OnSuccessAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception bookkeeping) {
            ResilienceTracing.RecordBookkeepingFailure(activity, ResilienceTracing.Outcomes.Success, bookkeeping);
        }
    }

    /// <summary>Records a failure; the operation's own exception is what the caller receives, whatever happens here.</summary>
    private static async ValueTask RecordFailureAsync(
        ICircuitBreaker circuitBreaker, string key, Activity? activity, Exception exception, CancellationToken cancellationToken) {

        ResilienceTracing.MarkFailure(activity, exception);

        try {
            await circuitBreaker.OnFailureAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception bookkeeping) {
            ResilienceTracing.RecordBookkeepingFailure(activity, ResilienceTracing.Outcomes.Failure, bookkeeping);
        }
    }
}
