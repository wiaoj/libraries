using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives;
public static partial class RangeExtensions {
    /// <summary>
    /// Calculates the absolute distance (length) between the <see cref="Range{T}.Min"/> and <see cref="Range{T}.Max"/> boundaries.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="INumber{T}"/>.</typeparam>
    /// <param name="range">The source range.</param>
    /// <returns>The difference between Max and Min.</returns>
    /// <example>
    /// <code>
    /// var range = new Range&lt;int&gt;(10, 20);
    /// int len = range.Length(); // 10
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Length<T>(this Range<T> range) where T : INumber<T> {
        return range.Max - range.Min;
    }

    /// <summary>
    /// Clamps the specified <paramref name="value"/> to be within the bounds of this range.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="INumber{T}"/>.</typeparam>
    /// <param name="range">The range used for clamping.</param>
    /// <param name="value">The value to constrain.</param>
    /// <returns>
    /// The <paramref name="value"/> if it is within the range; otherwise, 
    /// the nearest boundary (<see cref="Range{T}.Min"/> or <see cref="Range{T}.Max"/>).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Clamp<T>(this Range<T> range, T value) where T : INumber<T> {
        if(value < range.Min) return range.Min;
        if(value > range.Max) return range.Max;
        return value;
    }

    /// <summary>
    /// Calculates the numeric gap between two non-overlapping ranges.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="INumber{T}"/>.</typeparam>
    /// <param name="first">The first range.</param>
    /// <param name="second">The second range.</param>
    /// <returns>
    /// A new <see cref="Range{T}"/> representing the distance between the two ranges, 
    /// or <see langword="null"/> if the ranges overlap or are contiguous.
    /// </returns>
    /// <example>
    /// Range [1, 5] and [10, 15] have a gap of Range [5, 10].
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Range<T>? Gap<T>(this Range<T> first, Range<T> second) where T : INumber<T> {
        if(first.Overlaps(second)) return null;

        return first.Max < second.Min
            ? new Range<T>(first.Max, second.Min)
            : new Range<T>(second.Max, first.Min);
    }
}