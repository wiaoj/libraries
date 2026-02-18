using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives;

namespace Wiaoj.Extensions;

public static partial class TimeSpanExtensions {
    /// <param name="value">The long integer value to convert, representing ticks (100-nanosecond intervals).</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown if <paramref name="value"/> is negative.</exception>
    extension(long value) {
        /// <summary>
        /// Converts a <see cref="long"/> value to a <see cref="TimeSpan"/> representing ticks.
        /// This is the most direct and precise way to create a TimeSpan from a numeric value.
        /// </summary>
        /// <returns>A <see cref="TimeSpan"/> that represents the specified number of ticks.</returns>
        /// <exception cref="PrecaArgumentOutOfRangeException" />
        public TimeSpan Ticks() {
            Preca.ThrowIfNegative(value, () => new PrecaArgumentOutOfRangeException(nameof(value), "Ticks value cannot be negative.")); 
            return TimeSpan.FromTicks(value);
        }
    }
}