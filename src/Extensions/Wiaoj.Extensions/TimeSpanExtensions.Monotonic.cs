using System.Runtime.CompilerServices;
using Wiaoj.Primitives;

namespace Wiaoj.Extensions;

public static partial class TimeSpanExtensions {
    extension(TimeSpan timeSpan) {
        /// <summary>
        /// Calculates the future <see cref="MonotonicTimestamp"/> when this duration will expire,
        /// relative to the current monotonic time.
        /// </summary>
        /// <example>
        /// <code>
        /// MonotonicTimestamp due = 5.Seconds().FromNow();
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MonotonicTimestamp FromNow() {
            return MonotonicTimestamp.Now.Add(timeSpan);
        }

        /// <summary>
        /// Calculates the future <see cref="MonotonicTimestamp"/> when this duration will expire,
        /// relative to the provided <see cref="TimeProvider"/>.
        /// </summary>
        /// <param name="timeProvider">The time provider managing the monotonic clock.</param>
        /// <example>
        /// <code>
        /// MonotonicTimestamp due = 30.Milliseconds().FromNow(_timeProvider);
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MonotonicTimestamp FromNow(TimeProvider timeProvider) {
            Preca.ThrowIfNull(timeProvider);
            return MonotonicTimestamp.From(timeProvider).Add(timeSpan);
        }
    }
}