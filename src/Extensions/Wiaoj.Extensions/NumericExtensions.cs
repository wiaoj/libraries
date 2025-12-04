using System.Numerics;

namespace Wiaoj.Extensions;

public static class NumericExtensions {
    /// <summary>
    /// Provides an extension for numeric types to check if the value is zero.
    /// </summary>
    /// <typeparam name="T">
    /// The numeric type implementing <see cref="INumberBase{T}"/>.
    /// </typeparam>
    /// <param name="value">The numeric value to check.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is equal to <c>T.Zero</c>; otherwise, <see langword="false"/>.
    /// </returns>
    extension<T>(T value) where T : INumberBase<T> {
        /// <summary>
        /// Determines whether the numeric value is zero.
        /// </summary>
        public bool IsZero => value == T.Zero;
    }
}