using System.Runtime.CompilerServices;

namespace Wiaoj.Resilience;

/// <summary>
/// Defines an execution strategy that bounds asynchronous operations within a temporal deadline.
/// </summary>
public interface ITimeoutStrategy {
    /// <summary>
    /// Executes the specified asynchronous operation within the configured timeout boundary.
    /// </summary>
    /// <typeparam name="TResult">The operation return type.</typeparam>
    /// <param name="key">The identifier key of the target service or operation.</param>
    /// <param name="operation">The asynchronous delegate to execute.</param>
    /// <param name="cancellationToken">A token to observe for caller cancellation requests.</param>
    /// <returns>A task representing the result of the operation.</returns>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the configured deadline.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the caller cancels the operation before completion.</exception>
    [OverloadResolutionPriority(1)]
    ValueTask<TResult> ExecuteAsync<TResult>(
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a non-returning asynchronous operation within the configured timeout boundary.
    /// </summary>
    /// <param name="key">The identifier key of the target service or operation.</param>
    /// <param name="operation">The asynchronous delegate to execute.</param>
    /// <param name="cancellationToken">A token to observe for caller cancellation requests.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the configured deadline.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the caller cancels the operation before completion.</exception>
    ValueTask ExecuteAsync(
        string key,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default);
}

public interface ITimeoutStrategy<TPolicy> : ITimeoutStrategy where TPolicy : notnull;