using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives;

/// <summary>
/// Provides domain-specific extension methods for the <see cref="Range{T}"/> structure.
/// </summary>
/// <remarks>
/// This class extends <see cref="Range{T}"/> with specialized capabilities that become available 
/// depending on the underlying generic type <typeparamref name="T"/>. 
/// The extensions are logically grouped into the following categories:
/// <list type="bullet">
/// <item>
/// <description><b>Fluent / Generic:</b> General range evaluation and construction (<c>IsBetween</c>, <c>To</c>) for any <see cref="IComparable{T}"/>.</description>
/// </item>
/// <item>
/// <description><b>Numeric:</b> Mathematical operations like <c>Length</c>, <c>Clamp</c>, and <c>Gap</c> for types implementing <see cref="INumber{T}"/>.</description>
/// </item>
/// <item>
/// <description><b>Time:</b> Temporal calculations (e.g., <c>Duration</c>) and boundary checks (e.g., <c>IsPast</c>, <c>IsNowWithin</c>) for <see cref="DateTime"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="UnixTimestamp"/>, and <see cref="MonotonicTimestamp"/>.</description>
/// </item>
/// <item>
/// <description><b>Semantic Versioning:</b> Filtering and version resolution logic for <see cref="SemVer"/> types.</description>
/// </item>
/// <item>
/// <description><b>Percentage:</b> Proportional bounding and distance calculations for <see cref="Percentage"/> types.</description>
/// </item>
/// </list>
/// </remarks>
public static partial class RangeExtensions {
    /// <summary>
    /// Checks whether the value is within the specified inclusive interval [min, max].
    /// </summary>
    /// <typeparam name="T">The type of the comparable value.</typeparam>
    /// <param name="value">The value to evaluate.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns><see langword="true"/> if the value is between min and max (inclusive); otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBetween<T>(this T value, T min, T max) where T : IComparable<T> {
        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }

    /// <summary>
    /// Checks whether the value is within the specified inclusive <see cref="Range{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the comparable value.</typeparam>
    /// <param name="value">The value to evaluate.</param>
    /// <param name="range">The inclusive range interval.</param>
    /// <returns><see langword="true"/> if the value is contained in the range; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBetween<T>(this T value, Range<T> range) where T : IComparable<T> {
        return range.Contains(value);
    }

    /// <summary>
    /// Creates an inclusive <see cref="Range{T}"/> starting from this value to the specified upper bound.
    /// </summary>
    /// <typeparam name="T">The type of the comparable value.</typeparam>
    /// <param name="start">The start boundary of the range.</param>
    /// <param name="end">The end boundary of the range.</param>
    /// <returns>A new <see cref="Range{T}"/> interval.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Range<T> ToRange<T>(this T start, T end) where T : IComparable<T> {
        return Range<T>.Create(start, end);
    }
}