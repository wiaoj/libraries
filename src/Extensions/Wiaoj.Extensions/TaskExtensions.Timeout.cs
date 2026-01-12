using OperationTimeout = Wiaoj.Primitives.OperationTimeout;

namespace Wiaoj.Extensions;
/// <summary>
/// Provides extension methods for applying timeout policies to <see cref="Task"/> and <see cref="ValueTask"/>.
/// </summary>
public static class TaskTimeoutExtensions {
    /// <summary>
    /// Applies a timeout policy to a task. If the task does not complete within the timeout,
    /// a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    /// <param name="task">The task to which the timeout policy is applied.</param>
    /// <param name="timeout">The timeout policy.</param>
    /// <returns>A task that completes with the original task or throws a TimeoutException.</returns>
    /// <exception cref="TimeoutException">Thrown if the timeout is reached before the task completes.</exception>
    public static async Task WithTimeout(this Task task, OperationTimeout timeout) {
        // Eğer timeout anında iptal edilmişse veya sıfır ise, hemen kontrol et.
        if (timeout == OperationTimeout.Cancelled) throw new TaskCanceledException();
        if (timeout == OperationTimeout.None) {
            if (task.IsCompleted) { await task; return; }
            throw new TimeoutException("The operation timed out immediately (OperationTimeout.None).");
        }

        using CancellationTokenSource cts = timeout.CreateCancellationTokenSource();
        try {
            await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cts.Token.IsCancellationRequested) {
            // Token timeout yüzünden iptal olduysa TimeoutException fırlat
            throw new TimeoutException("The operation has timed out.");
        }
    }

    /// <summary>
    /// Applies a timeout policy to a generic task. If the task does not complete within the timeout,
    /// a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, OperationTimeout timeout) { 
        if (timeout == OperationTimeout.Cancelled) throw new TaskCanceledException();
        if (timeout == OperationTimeout.None) {
            if (task.IsCompleted) return await task;
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
    /// Applies a timeout policy to a ValueTask.
    /// </summary>
    public static async ValueTask WithTimeout(this ValueTask task, OperationTimeout timeout) {
        if (task.IsCompletedSuccessfully) {
            await task; // Olası senkron exception'ları yakalamak için.
            return;
        }

        // Eğer senkron tamamlanmadıysa, onu Task'e dönüştürüp standart mantığı uygula.
        // Bu bir miktar ek yüke (allocation) neden olabilir, ancak kaçınılmazdır.
        await task.AsTask().WithTimeout(timeout);
    }

    /// <summary>
    /// Applies a timeout policy to a generic ValueTask.
    /// </summary>
    public static async ValueTask<TResult> WithTimeout<TResult>(this ValueTask<TResult> task, OperationTimeout timeout) {
        if (task.IsCompletedSuccessfully) {
            return task.Result;
        }

        return await task.AsTask().WithTimeout(timeout);
    }
}