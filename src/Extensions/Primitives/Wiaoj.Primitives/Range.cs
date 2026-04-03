using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents an inclusive interval [Min, Max] that ensures the start is always less than or equal to the end.
/// Supports any type that implements <see cref="IComparable{T}"/>.
/// </summary>
/// <typeparam name="T">The type of value (e.g. <see cref="int"/>, <see cref="DateTime"/>, <see cref="BigInteger"/>).</typeparam>
[DebuggerDisplay("[{Min}, {Max}]")]
[JsonConverter(typeof(RangeJsonConverterFactory))]
public readonly record struct Range<T> : IEquatable<Range<T>> where T : IComparable<T> {
    /// <summary>Gets the inclusive lower bound of the range.</summary>
    public T Min { get; }

    /// <summary>Gets the inclusive upper bound of the range.</summary>
    public T Max { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Range{T}"/> struct with strict boundary validation.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the range.</param>
    /// <param name="max">The inclusive upper bound of the range.</param>
    /// <remarks>
    /// This constructor enforces strict ordering. If <paramref name="min"/> is greater than <paramref name="max"/>, 
    /// a validation exception is thrown instead of automatically swapping the values.
    /// </remarks>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="min"/> or <paramref name="max"/> is null.</exception>
    /// <exception cref="PrecaArgumentException">
    /// Thrown when <paramref name="min"/> is greater than <paramref name="max"/>, 
    /// or when a floating-point value is <see cref="double.NaN"/> or <see cref="float.NaN"/>.
    /// </exception>
    public Range(T min, T max) { 
        Preca.ThrowIfNull(min);
        Preca.ThrowIfNull(max);
         
        if(min is double dMin) Preca.ThrowIfNaN(dMin);
        if(max is double dMax) Preca.ThrowIfNaN(dMax);
        if(min is float fMin) Preca.ThrowIfNaN(fMin);
        if(max is float fMax) Preca.ThrowIfNaN(fMax);
        if(min is Half hMin) Preca.ThrowIfNaN(hMin);
        if(max is Half hMax) Preca.ThrowIfNaN(hMax);


        Preca.ThrowIf(
            min.CompareTo(max) > 0,
            static (x) => new PrecaArgumentException($"Min value ({x.min}) cannot be greater than Max value ({x.max})."), (min,max));

        this.Min = min;
        this.Max = max;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Range{T}"/> struct using a standard C# <see cref="System.Range"/>.
    /// </summary>
    /// <param name="range">The range expression (e.g., <c>10..50</c>).</param>
    /// <exception cref="PrecaInvalidOperationException">Thrown when <typeparamref name="T"/> is not <see cref="int"/>.</exception>
    /// <exception cref="PrecaArgumentException">Thrown if the provided <paramref name="range"/> uses relative-from-end indices.</exception>
    public Range(System.Range range) {
        Preca.ThrowIfGenericTypeIsNot<T, int, PrecaInvalidOperationException>(() =>
            new PrecaInvalidOperationException($"Conversion from System.Range is only supported for Range<int>."));

        Preca.ThrowIf(range.Start.IsFromEnd || range.End.IsFromEnd,
            static () => new PrecaArgumentException("Domain Range does not support 'from-end' (^) indices. Use absolute values."));

        int start = range.Start.Value;
        int end = range.End.Value;

        // Zero-allocation casting for int
        T min = Unsafe.As<int, T>(ref start);
        T max = Unsafe.As<int, T>(ref end);

        if(min.CompareTo(max) > 0) {
            this.Min = max;
            this.Max = min;
        }
        else {
            this.Min = min;
            this.Max = max;
        }
    }

    /// <summary>
    /// Creates a new <see cref="Range{T}"/> instance by automatically determining the minimum and maximum values 
    /// from the provided arguments.
    /// </summary>
    /// <param name="val1">The first value to compare.</param>
    /// <param name="val2">The second value to compare.</param>
    /// <returns>
    /// A <see cref="Range{T}"/> where the smaller value is assigned to <see cref="Min"/> 
    /// and the larger value to <see cref="Max"/>.
    /// </returns>
    /// <remarks>
    /// Unlike the constructor, this factory method is "self-healing" and will not throw an exception 
    /// if the first argument is larger than the second; it will simply swap them.
    /// </remarks>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="val1"/> or <paramref name="val2"/> is null.</exception>
    public static Range<T> Create(T val1, T val2) {
        Preca.ThrowIfNull(val1);
        Preca.ThrowIfNull(val2);

        return val1.CompareTo(val2) <= 0
            ? new Range<T>(val1, val2)
            : new Range<T>(val2, val1);
    }

    /// <summary>
    /// A semantic alias for <see cref="Create"/> that defines a range encompassing all values between <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    /// <param name="a">The first boundary value.</param>
    /// <param name="b">The second boundary value.</param>
    /// <returns>A validated <see cref="Range{T}"/> instance.</returns>
    /// <remarks>
    /// This method provides a more natural syntax for scenarios where the boundaries are inclusive and 
    /// the order of input is not guaranteed.
    /// </remarks>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="a"/> or <paramref name="b"/> is null.</exception>
    public static Range<T> Between(T a, T b) {
        return Create(a, b);
    }

    /// <summary>Determines whether the specified value is within the inclusive range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T value) {
        return value.CompareTo(this.Min) >= 0 && value.CompareTo(this.Max) <= 0;
    }

    /// <summary>Determines whether the specified range is entirely contained within this range.</summary>
    public bool Contains(Range<T> other) {
        return other.Min.CompareTo(this.Min) >= 0 && other.Max.CompareTo(this.Max) <= 0;
    }

    /// <summary>Checks if this range overlaps with another range.</summary>
    public bool Overlaps(Range<T> other) {
        return this.Min.CompareTo(other.Max) <= 0 && this.Max.CompareTo(other.Min) >= 0;
    }

    /// <summary>Returns the intersection of two ranges, or <see langword="null"/> if they do not overlap.</summary>
    public Range<T>? Intersect(Range<T> other) {
        if(!Overlaps(other)) return null;
        T newMin = this.Min.CompareTo(other.Min) > 0 ? this.Min : other.Min;
        T newMax = this.Max.CompareTo(other.Max) < 0 ? this.Max : other.Max;
        return new Range<T>(newMin, newMax);
    }

    /// <summary>Returns the smallest range that encompasses both ranges.</summary>
    public Range<T> Union(Range<T> other) {
        T newMin = this.Min.CompareTo(other.Min) < 0 ? this.Min : other.Min;
        T newMax = this.Max.CompareTo(other.Max) > 0 ? this.Max : other.Max;
        return new Range<T>(newMin, newMax);
    }

    /// <inheritdoc/>
    public override string ToString() {
        return $"[{this.Min}, {this.Max}]";
    }

    /// <summary>Deconstructs the range into its Min and Max components.</summary>
    public void Deconstruct(out T min, out T max) {
        min = this.Min;
        max = this.Max;
    }
}