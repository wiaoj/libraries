using OperationTimeout = Wiaoj.Primitives.OperationTimeout;

namespace Wiaoj.Extensions;

/// <summary>
/// Provides extension methods for applying unified timeout and cancellation policies 
/// to <see cref="Task"/> and <see cref="ValueTask"/> instances using <see cref="OperationTimeout"/>.
/// </summary>
public static class TaskTimeoutExtensions {
    /// <summary>
    /// Applies a timeout policy to a <see cref="Task"/>. If the task does not complete within the specified timeout,
    /// a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    /// <param name="task">The underlying asynchronous task to await with a timeout constraint.</param>
    /// <param name="timeout">The timeout policy containing duration and/or linked cancellation tokens.</param>
    /// <returns>A task that represents the asynchronous operation bounded by the timeout.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="task"/> is <see langword="null"/>.</exception>
    /// <exception cref="TaskCanceledException">Thrown if <paramref name="timeout"/> was already canceled before the operation started.</exception>
    /// <exception cref="TimeoutException">Thrown if the timeout period elapses before the task completes.</exception>
    /// <example>
    /// <code>
    /// await DoWorkAsync().WithTimeout(OperationTimeout.FromSeconds(5));
    /// </code>
    /// </example>
    public static async Task WithTimeout(this Task task, OperationTimeout timeout) {
        Preca.ThrowIfNull(task);

        if(timeout == OperationTimeout.Cancelled) throw new TaskCanceledException();
        if(timeout == OperationTimeout.None) {
            if(task.IsCompleted) {
                await task.ConfigureAwait(false);
                return;
            }
            throw new TimeoutException("The operation timed out immediately (OperationTimeout.None).");
        }

        using CancellationTokenSource cts = timeout.CreateCancellationTokenSource();
        try {
            await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cts.Token.IsCancellationRequested) {
            throw new TimeoutException("The operation has timed out.");
        }
    }

    /// <summary>
    /// Applies a timeout policy to a generic <see cref="Task{TResult}"/>. If the task does not complete within the specified timeout,
    /// a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the task.</typeparam>
    /// <param name="task">The underlying asynchronous task to await with a timeout constraint.</param>
    /// <param name="timeout">The timeout policy containing duration and/or linked cancellation tokens.</param>
    /// <returns>A task representing the result of the asynchronous operation if completed in time.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="task"/> is <see langword="null"/>.</exception>
    /// <exception cref="TaskCanceledException">Thrown if <paramref name="timeout"/> was already canceled before the operation started.</exception>
    /// <exception cref="TimeoutException">Thrown if the timeout period elapses before the task completes.</exception>
    /// <example>
    /// <code>
    /// string result = await FetchDataAsync().WithTimeout(OperationTimeout.FromSeconds(3));
    /// </code>
    /// </example>
    public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, OperationTimeout timeout) {
        Preca.ThrowIfNull(task);

        if(timeout == OperationTimeout.Cancelled) throw new TaskCanceledException();
        if(timeout == OperationTimeout.None) {
            if(task.IsCompleted) return await task.ConfigureAwait(false);
            throw new TimeoutException("The operation timed out immediately (OperationTimeout.None).");
        }

        using CancellationTokenSource cts = timeout.CreateCancellationTokenSource();
        try {
            return await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cts.Token.IsCancellationRequested) {
            throw new TimeoutException("The operation has timed out.");
        }
    }

    // --- ValueTask Overloads ---

    /// <summary>
    /// Applies a timeout policy to a <see cref="ValueTask"/>. If the task does not complete within the specified timeout,
    /// a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    /// <param name="task">The <see cref="ValueTask"/> to await with a timeout constraint.</param>
    /// <param name="timeout">The timeout policy containing duration and/or linked cancellation tokens.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation bounded by the timeout.</returns>
    /// <exception cref="TaskCanceledException">Thrown if <paramref name="timeout"/> was already canceled before the operation started.</exception>
    /// <exception cref="TimeoutException">Thrown if the timeout period elapses before the task completes.</exception>
    public static async ValueTask WithTimeout(this ValueTask task, OperationTimeout timeout) {
        if(task.IsCompletedSuccessfully) {
            await task.ConfigureAwait(false);
            return;
        }

        await task.AsTask().WithTimeout(timeout).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a timeout policy to a generic <see cref="ValueTask{TResult}"/>. If the task does not complete within the specified timeout,
    /// a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the task.</typeparam>
    /// <param name="task">The <see cref="ValueTask{TResult}"/> to await with a timeout constraint.</param>
    /// <param name="timeout">The timeout policy containing duration and/or linked cancellation tokens.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the result of the asynchronous operation if completed in time.</returns>
    /// <exception cref="TaskCanceledException">Thrown if <paramref name="timeout"/> was already canceled before the operation started.</exception>
    /// <exception cref="TimeoutException">Thrown if the timeout period elapses before the task completes.</exception>
    public static async ValueTask<TResult> WithTimeout<TResult>(this ValueTask<TResult> task, OperationTimeout timeout) {
        if(task.IsCompletedSuccessfully) {
            return task.Result;
        }

        return await task.AsTask().WithTimeout(timeout).ConfigureAwait(false);
    }
}