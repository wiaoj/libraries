using System.Runtime.CompilerServices;
using Wiaoj.Primitives;

namespace Wiaoj.Extensions;

public static partial class TimeSpanExtensions {
    /// <param name="timeSpan">The base TimeSpan to which the jitter will be applied.</param>
    extension(TimeSpan timeSpan) {
        /// <summary>
        /// Applies a random deviation, or "jitter," to the current TimeSpan value. 
        /// </summary>
        /// <param name="percentage">The maximum percentage of deviation, represented as a <see cref="Percentage"/> instance.</param>
        /// <returns>
        /// A new TimeSpan instance with the applied jitter. The resulting duration will be randomized
        /// within +/- the specified percentage of the original TimeSpan, but will not be less than zero.
        /// </returns>
        /// <remarks>
        /// This method is useful for preventing multiple concurrent operations from executing at the exact same time
        /// by introducing a small, random variance in their delay. 
        /// Example: `5.TotalSeconds().WithJitter(Jitter.Medium)` will return a TimeSpan between 4.5 and 5.5 seconds.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan WithJitter(Percentage percentage) {
            if (timeSpan.Ticks is 0 || percentage.IsZero)
                return timeSpan;

            double baseTicks = timeSpan.Ticks;
            double p = percentage.Value;

            double randomFactor = Random.Shared.NextDouble(-1.0, 1.0);

            double jitteredTicks = baseTicks + (baseTicks * p * randomFactor);

            return ((long)Math.Max(0, jitteredTicks)).Ticks();
        }
    }
}