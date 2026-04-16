using System.Diagnostics;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents a strict growth multiplier (must be strictly greater than 1.0).
/// </summary>
[DebuggerDisplay("x{Value}")]
public readonly record struct GrowthRate {
    public double Value { get; }

    /// <summary> Default growth rate of x2.0 </summary>
    public static GrowthRate Double { get; } = new(2.0);

    /// <summary> Growth rate of x1.5 </summary>
    public static GrowthRate OneAndHalf { get; } = new(1.5);

    private GrowthRate(double value) {
        this.Value = value;
    }

    public static GrowthRate FromDouble(double value) {
        Preca.ThrowIfLessThanOrEqualTo(
            value,
            1,
            () => new ArgumentOutOfRangeException(nameof(value), "Growth rate must be strictly greater than 1.0."));

        return new GrowthRate(value);
    }

    public static implicit operator double(GrowthRate rate) {
        return rate.Value;
    }

    public static explicit operator GrowthRate(double value) {
        return FromDouble(value);
    }
}