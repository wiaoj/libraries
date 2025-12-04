namespace Wiaoj.Extensions;

public static partial class TimeSpanExtensions {
    /// <param name="value">The integer value to convert, representing milliseconds.</param> 
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown if <paramref name="value"/> is negative.</exception>
    extension(int value) {
        /// <summary>
        /// Converts an <see cref="int"/> value to a <see cref="TimeSpan"/> representing milliseconds.
        /// </summary> 
        /// <returns>A <see cref="TimeSpan"/> that represents the specified number of milliseconds.</returns>    
        /// <exception cref="PrecaArgumentOutOfRangeException" />
        public TimeSpan Milliseconds() {
            Preca.ThrowIfNegative(value);
            return TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        /// Converts an <see cref="int"/> value to a <see cref="TimeSpan"/> representing seconds.
        /// </summary> 
        /// <returns>A <see cref="TimeSpan"/> that represents the specified number of seconds.</returns> 
        /// <exception cref="PrecaArgumentOutOfRangeException" />
        public TimeSpan Seconds() {
            Preca.ThrowIfNegative(value);
            return TimeSpan.FromSeconds(value);
        }

        /// <summary>
        /// Converts an <see cref="int"/> value to a <see cref="TimeSpan"/> representing minutes.
        /// </summary> 
        /// <returns>A <see cref="TimeSpan"/> that represents the specified number of minutes.</returns> 
        /// <exception cref="PrecaArgumentOutOfRangeException" />
        public TimeSpan Minutes() {
            Preca.ThrowIfNegative(value);
            return TimeSpan.FromMinutes(value);
        }

        /// <summary>
        /// Converts an <see cref="int"/> value to a <see cref="TimeSpan"/> representing hours.
        /// </summary> 
        /// <returns>A <see cref="TimeSpan"/> that represents the specified number of hours.</returns> 
        /// <exception cref="PrecaArgumentOutOfRangeException" />
        public TimeSpan Hours() {
            Preca.ThrowIfNegative(value);
            return TimeSpan.FromHours(value);
        }

        /// <summary>
        /// Converts an <see cref="int"/> value to a <see cref="TimeSpan"/> representing days.
        /// </summary> 
        /// <returns>A <see cref="TimeSpan"/> that represents the specified number of days.</returns> 
        /// <exception cref="PrecaArgumentOutOfRangeException" />
        public TimeSpan Days() {
            Preca.ThrowIfNegative(value);
            return TimeSpan.FromDays(value);
        }
    }
}