namespace Wiaoj.Extensions;

public static partial class TimeSpanExtensions {
    extension(TimeSpan) {
        /// <summary>
        /// Returns the smaller of two TimeSpan values.
        /// </summary>
        /// <param name="first">The first TimeSpan to compare.</param>
        /// <param name="second">The second TimeSpan to compare.</param>
        /// <returns>The smaller of the two TimeSpan values.</returns>
        public static TimeSpan Min(TimeSpan first, TimeSpan second) {
            return first < second ? first : second;
        }

        /// <summary>
        /// Returns the larger of two TimeSpan values.
        /// </summary>
        /// <param name="first">The first TimeSpan to compare.</param>
        /// <param name="second">The second TimeSpan to compare.</param>
        /// <returns>The larger of the two TimeSpan values.</returns>
        public static TimeSpan Max(TimeSpan first, TimeSpan second) {
            return first > second ? first : second;
        }

        /// <summary>
        /// Returns the smallest <see cref="TimeSpan"/> from a set of values.
        /// </summary>
        /// <param name="timeSpans">An array of <see cref="TimeSpan"/> values to compare.</param>
        /// <returns>The smallest <see cref="TimeSpan"/> in the array.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="timeSpans"/> is null or empty.</exception>
        public static TimeSpan Min(params Span<TimeSpan> timeSpans) {
            Preca.ThrowIfEmpty(timeSpans, static () => new ArgumentException("At least one TimeSpan must be provided."));

            TimeSpan min = timeSpans[0];
            foreach (TimeSpan ts in timeSpans) {
                if (ts < min)
                    min = ts;
            }
            return min;
        }

        /// <summary>
        /// Returns the largest <see cref="TimeSpan"/> from a set of values.
        /// </summary>
        /// <param name="timeSpans">An array of <see cref="TimeSpan"/> values to compare.</param>
        /// <returns>The largest <see cref="TimeSpan"/> in the array.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="timeSpans"/> is null or empty.</exception>
        public static TimeSpan Max(params Span<TimeSpan> timeSpans) {
            Preca.ThrowIfEmpty(timeSpans, static () => new ArgumentException("At least one TimeSpan must be provided."));

            TimeSpan max = timeSpans[0];
            foreach (TimeSpan ts in timeSpans) {
                if (ts > max)
                    max = ts;
            }
            return max;
        }
    }
}