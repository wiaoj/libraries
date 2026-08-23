using System.Runtime.CompilerServices;

namespace Wiaoj.Extensions;

public static class TimeProviderExtensions {
    extension(TimeProvider timeProvider) {
        /// <summary>
        /// Creates a task that completes after a specified time interval using this <see cref="TimeProvider"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default) {
            return Task.Delay(delay, timeProvider, cancellationToken);
        }

        /// <summary>
        /// Creates a task that completes after a specified number of milliseconds using this <see cref="TimeProvider"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task Delay(int millisecondsDelay, CancellationToken cancellationToken = default) {
            return Task.Delay(TimeSpan.FromMilliseconds(millisecondsDelay), timeProvider, cancellationToken);
        }
    }
}