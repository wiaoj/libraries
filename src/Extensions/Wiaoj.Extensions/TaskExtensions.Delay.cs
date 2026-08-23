using Wiaoj.Primitives;


namespace Wiaoj.Extensions;
/// <summary>
/// Provides extension methods for asynchronous delays using the <see cref="OperationTimeout"/> primitive.
/// </summary>
public static class TaskDelayExtensions {
    /// <summary>
    /// Creates a task that completes after a specified timeout, allowing for combined time-based
    /// and token-based cancellation.
    /// </summary>
    /// <param name="timeout">
    /// The timeout policy that defines the duration of the delay and/or a cancellation token to observe.
    /// </param>
    /// <returns>A task that represents the asynchronous delay operation.</returns>
    /// <remarks>
    /// This method serves as a powerful, unified alternative to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// It leverages the <see cref="OperationTimeout.CreateCancellationTokenSource()"/> method to handle the underlying cancellation logic.
    /// <example>
    /// <code>
    /// // Wait for 5 seconds
    /// await OperationTimeout.FromSeconds(5).DelayAsync();
    ///
    /// // Wait for 30 seconds or until a token is cancelled
    /// await OperationTimeout.FromMilliseconds(30.TotalSeconds(), myToken).DelayAsync();
    /// </code>
    /// </example>
    /// </remarks>
    public static async Task DelayAsync(this OperationTimeout timeout) {
        CancellationTokenSource cts = timeout.CreateCancellationTokenSource();
        await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token).ConfigureAwait(false);
    }
}