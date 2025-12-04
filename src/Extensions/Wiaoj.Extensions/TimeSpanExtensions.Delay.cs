using System.Runtime.CompilerServices;

namespace Wiaoj.Extensions;

public static partial class TimeSpanExtensions {
    /// <param name="timeSpan">The time interval to wait for.</param>
    extension(TimeSpan timeSpan) {
        /// <summary>
        /// Asynchronously waits for the duration specified by the TimeSpan.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A Task that represents the asynchronous delay operation.</returns>
        /// <remarks>This is a convenience extension method that wraps <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task Delay(CancellationToken cancellationToken = default) {
            return Delay(timeSpan, TimeProvider.System, cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits for the duration specified by the TimeSpan, using the provided <see cref="TimeProvider"/> to control the passage of time.
        /// </summary> 
        /// <param name="timeProvider">The <see cref="TimeProvider"/> that will be used to manage the delay.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A Task that represents the asynchronous delay operation.</returns>
        /// <remarks>
        /// This overload wraps <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> and is particularly useful 
        /// for testing scenarios where time can be controlled or simulated.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task Delay(TimeProvider timeProvider, CancellationToken cancellationToken = default) {
            return Task.Delay(timeSpan, timeProvider, cancellationToken);
        }
    }
}