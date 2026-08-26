using System.Runtime.CompilerServices;
using Wiaoj.Preconditions;

namespace Wiaoj.Resilience;

/// <summary>
/// Extension methods for executing asynchronous delegates under timeout protection with fallback capabilities.
/// </summary>
public static class TimeoutExecutionExtensions {
    /// <summary>
    /// Executes a delegate under timeout protection, returning a static fallback value if timed out or failed.
    /// </summary>
    public static async ValueTask<TResult> ExecuteWithFallbackAsync<TResult>(
        this ITimeoutStrategy timeoutStrategy,
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        TResult fallbackValue,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(timeoutStrategy);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);

        try {
            return await timeoutStrategy.ExecuteAsync(key, operation, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch {
            return fallbackValue;
        }
    }

    /// <summary>
    /// Executes a delegate under timeout protection, invoking an asynchronous fallback factory if timed out or failed.
    /// </summary>
    [OverloadResolutionPriority(1)]
    public static async ValueTask<TResult> ExecuteWithFallbackAsync<TResult>(
        this ITimeoutStrategy timeoutStrategy,
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        Func<Exception, CancellationToken, ValueTask<TResult>> fallbackFactory,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(timeoutStrategy);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);
        Preca.ThrowIfNull(fallbackFactory);

        try {
            return await timeoutStrategy.ExecuteAsync(key, operation, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception ex) {
            return await fallbackFactory(ex, cancellationToken).ConfigureAwait(false);
        }
    }
}