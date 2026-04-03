using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives;
public static partial class RangeExtensions {
    /// <summary>Calculates the duration between the start and end of the <see cref="DateTime"/> range.</summary>
    /// <param name="range">The range to calculate.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the total duration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Duration(this Range<DateTime> range) {
        return range.Max - range.Min;
    }

    /// <summary>Calculates the duration between the start and end of the <see cref="DateTimeOffset"/> range.</summary>
    /// <param name="range">The range to calculate.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the total duration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Duration(this Range<DateTimeOffset> range) {
        return range.Max - range.Min;
    }

    /// <summary>Calculates the duration between the start and end of the <see cref="TimeOnly"/> range.</summary>
    /// <param name="range">The range to calculate.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the total duration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Duration(this Range<TimeOnly> range) {
        return range.Max - range.Min;
    }

    /// <summary>Calculates the duration between the start and end of the <see cref="UnixTimestamp"/> range.</summary>
    /// <param name="range">The range to calculate.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the total duration in milliseconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Duration(this Range<UnixTimestamp> range) {
        return TimeSpan.FromMilliseconds(range.Max.TotalMilliseconds - range.Min.TotalMilliseconds);
    }

    /// <summary>Calculates the total number of days between the start and end of the <see cref="DateOnly"/> range.</summary>
    /// <param name="range">The range to calculate.</param>
    /// <returns>An integer representing the day count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DurationDays(this Range<DateOnly> range) {
        return range.Max.DayNumber - range.Min.DayNumber;
    }


    /// <summary>Checks if the entire time range is in the past (i.e., Max is strictly less than current UTC time).</summary>
    /// <param name="range">The range to check.</param>
    /// <returns><see langword="true"/> if the end of the range is before <see cref="DateTime.UtcNow"/>.</returns>
    public static bool IsPast(this Range<DateTime> range) {
        return range.Max < DateTime.UtcNow;
    }

    /// <inheritdoc cref="IsPast(Range{DateTime})"/>
    public static bool IsPast(this Range<DateTimeOffset> range) {
        return range.Max < DateTimeOffset.UtcNow;
    }

    /// <inheritdoc cref="IsPast(Range{DateTime})"/>
    public static bool IsPast(this Range<DateOnly> range) {
        return range.Max < DateOnly.FromDateTime(DateTime.UtcNow);
    }

    /// <summary>Checks if the entire time range is in the future (i.e., Min is strictly greater than current UTC time).</summary>
    /// <param name="range">The range to check.</param>
    /// <returns><see langword="true"/> if the start of the range is after <see cref="DateTime.UtcNow"/>.</returns>
    public static bool IsFuture(this Range<DateTime> range) {
        return range.Min > DateTime.UtcNow;
    }

    /// <inheritdoc cref="IsFuture(Range{DateTime})"/>
    public static bool IsFuture(this Range<DateTimeOffset> range) {
        return range.Min > DateTimeOffset.UtcNow;
    }

    /// <inheritdoc cref="IsFuture(Range{DateTime})"/>
    public static bool IsFuture(this Range<DateOnly> range) {
        return range.Min > DateOnly.FromDateTime(DateTime.UtcNow);
    }


    /// <summary>Checks if the current UTC time falls within the inclusive range.</summary>
    /// <param name="range">The range to check.</param>
    /// <returns><see langword="true"/> if <see cref="DateTime.UtcNow"/> is within bounds.</returns>
    public static bool IsNowWithin(this Range<DateTime> range) {
        return range.Contains(DateTime.UtcNow);
    }

    /// <inheritdoc cref="IsNowWithin(Range{DateTime})"/>
    public static bool IsNowWithin(this Range<DateTimeOffset> range) {
        return range.Contains(DateTimeOffset.UtcNow);
    }

    /// <summary>Checks if the current UTC date falls within the inclusive range.</summary>
    /// <param name="range">The range to check.</param>
    /// <returns><see langword="true"/> if today's date is within bounds.</returns>
    public static bool IsTodayWithin(this Range<DateOnly> range) {
        return range.Contains(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}