using System.Diagnostics;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents a strict growth multiplier (must be strictly greater than 1.0).
/// </summary>
[DebuggerDisplay("x{Value}")]
public readonly record struct GrowthRate {
    /// <summary>
    /// Gets the raw numeric value of the growth rate.
    /// </summary>
    public double Value { get; }

    /// <summary> Default growth rate of x2.0 </summary>
    public static GrowthRate Double { get; } = new(2.0);

    /// <summary> Growth rate of x1.5 </summary>
    public static GrowthRate OneAndHalf { get; } = new(1.5);

    private GrowthRate(double value) {
        this.Value = value;
    }

    /// <summary>
    /// Creates a <see cref="GrowthRate"/> from a double value.
    /// </summary>
    /// <param name="value">The growth multiplier. Must be strictly greater than 1.0.</param>
    /// <returns>A validated <see cref="GrowthRate"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if value is less than or equal to 1.0.</exception>
    public static GrowthRate FromDouble(double value) {
        Preca.ThrowIfLessThanOrEqualTo(
            value,
            1,
            () => new ArgumentOutOfRangeException(nameof(value), "Growth rate must be strictly greater than 1.0."));

        return new GrowthRate(value);
    }

    /// <summary>
    /// Implicitly converts a <see cref="GrowthRate"/> to a double.
    /// </summary>
    /// <param name="rate">The growth rate to convert.</param>
    public static implicit operator double(GrowthRate rate) {
        return rate.Value;
    }

    /// <summary>
    /// Explicitly converts a double to a <see cref="GrowthRate"/>.
    /// </summary>
    /// <param name="value">The double value to convert.</param>
    public static explicit operator GrowthRate(double value) {
        return FromDouble(value);
    }
}