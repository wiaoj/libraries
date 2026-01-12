using System.Numerics;
using Wiaoj.Primitives;

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

    /// <summary>
    /// Interprets the long value as <see cref="UnixTimestamp"/> in MILLISECONDS.
    /// <para>Example: <c>1715500000000.ToUnixTimestamp()</c></para>
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds that have elapsed since 1970-01-01T00:00:00Z.</param>
    /// <returns>A strongly-typed <see cref="UnixTimestamp"/>.</returns>
    public static UnixTimestamp ToUnixTimestamp(this long milliseconds) {
        return UnixTimestamp.From(milliseconds);
    }
}