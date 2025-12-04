using System.Runtime.CompilerServices;
using Wiaoj.Primitives;

namespace Wiaoj.Extensions;
/// <summary>
/// Provides extension methods for the <see cref="Random"/> class.
/// </summary>
public static class RandomExtensions {
    extension(Random random) {
        /// <summary>
        /// Returns a random percentage value between 0.0 (inclusive) and 1.0 (inclusive).
        /// </summary> 
        /// <returns>
        /// A new <see cref="Percentage"/> instance with a random value that can include
        /// both 0% and 100%.
        /// </returns>
        /// <remarks>
        /// This method generates an integer between 0 and 100 (inclusive) and divides it by 100.0
        /// to ensure that the full range of percentage values, including exactly 1.0, is achievable.
        /// The precision of the result is up to two decimal places (e.g., 0.01, 0.52, 1.00).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Percentage NextPercentage() {
            int randomValue = random.Next(0, 101);
            return Percentage.FromInt(randomValue);
        }

        /// <summary>
        /// Returns a random percentage value between 0.0 (inclusive) and a specified maximum value (inclusive).
        /// </summary> 
        /// <param name="maxValue">The maximum percentage value to be returned.</param>
        /// <returns>A new <see cref="Percentage"/> instance with a random value within the specified range.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Percentage NextPercentage(Percentage maxValue) {
            int maxInt = (int)(maxValue.Value * 100);

            int randomValue = random.Next(0, maxInt + 1);

            return Percentage.FromInt(randomValue);
        }

        /// <summary>
        /// Returns a random double-precision floating-point number that is within a specified range.
        /// The range is inclusive of the minimum value and exclusive of the maximum value.
        /// </summary> 
        /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
        /// <param name="maxValue">The exclusive upper bound of the random number returned.</param>
        /// <returns>A double-precision floating-point number greater than or equal to minValue and less than maxValue.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double NextDouble(double minValue, double maxValue) {
            return random.NextDouble() * (maxValue - minValue) + minValue;
        }
    }
}