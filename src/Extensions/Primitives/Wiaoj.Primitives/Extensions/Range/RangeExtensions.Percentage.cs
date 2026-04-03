using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives;
public static partial class RangeExtensions {
    /// <summary>
    /// Calculates the absolute distance (length) between the Min and Max percentages.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Percentage Length(this Range<Percentage> range) { 
        return Percentage.FromDouble(range.Max.Value - range.Min.Value);
    }

    /// <summary>
    /// Clamps the specified percentage to be within the bounds of this range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Percentage Clamp(this Range<Percentage> range, Percentage value) {
        if(value < range.Min) return range.Min;
        if(value > range.Max) return range.Max;
        return value;
    }

    /// <summary>
    /// Calculates the gap between two non-overlapping percentage ranges.
    /// </summary>
    public static Range<Percentage>? Gap(this Range<Percentage> first, Range<Percentage> second) {
        if(first.Overlaps(second)) return null;

        return first.Max < second.Min
            ? new Range<Percentage>(first.Max, second.Min)
            : new Range<Percentage>(second.Max, first.Min);
    }
}