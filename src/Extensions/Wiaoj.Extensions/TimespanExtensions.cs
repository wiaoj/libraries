using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
            foreach(TimeSpan ts in timeSpans) {
                if(ts < min)
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
        public static TimeSpan Max(params ReadOnlySpan<TimeSpan> timeSpans) {
            Preca.ThrowIfEmpty(timeSpans, static () => new ArgumentException("At least one TimeSpan must be provided."));

            TimeSpan max = timeSpans[0];
            foreach(TimeSpan ts in timeSpans) {
                if(ts > max)
                    max = ts;
            }
            return max;
        }
    }

    extension([NotNullWhen(false)] TimeSpan? timeSpan) {
        /// <summary>
        /// Determines whether the nullable <see cref="TimeSpan"/> is null, or if its value is less than or equal to <see cref="TimeSpan.Zero"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value is null or less than or equal to zero; otherwise, <see langword="false"/>.
        /// </returns>
        public bool IsNullOrLessThanOrEqualToZero() {
            return timeSpan is null || timeSpan.Value <= TimeSpan.Zero;
        }
    }

    extension(TimeSpan timeSpan) {
        /// <summary>
        /// Returns this <see cref="TimeSpan"/> if it is strictly positive (greater than zero);
        /// otherwise returns the specified <paramref name="fallback"/> duration.
        /// </summary>
        /// <param name="value">The time span to validate.</param>
        /// <param name="fallback">The fallback duration to use if zero or negative.</param>
        /// <returns>A guaranteed positive <see cref="TimeSpan"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ToPositiveOrDefault(TimeSpan fallback) {
            return timeSpan > TimeSpan.Zero ? timeSpan : fallback;
        }
    }

    extension(TimeSpan? timeSpan) {
        /// <summary>
        /// Returns the active <see cref="TimeSpan"/> if it has a value and is strictly positive (greater than zero);
        /// otherwise returns the specified <paramref name="fallback"/> duration.
        /// </summary>
        /// <param name="value">The nullable time span to validate.</param>
        /// <param name="fallback">The fallback duration to use if null, zero, or negative.</param>
        /// <returns>A guaranteed positive <see cref="TimeSpan"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ToPositiveOrDefault(TimeSpan fallback) {
            return timeSpan is { } ts && ts > TimeSpan.Zero ? ts : fallback;
        }
    }
}