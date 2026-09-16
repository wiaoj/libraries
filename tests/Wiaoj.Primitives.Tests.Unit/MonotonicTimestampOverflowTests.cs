using System.Diagnostics;
using System.Numerics;

namespace Wiaoj.Primitives.Tests.Unit;

/// <summary>
/// Monotonic arithmetic scales with integer math and throws instead of wrapping around (#170).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Primitives")]
[Trait("Component", "MonotonicTimestamp")]
public sealed class MonotonicTimestampOverflowTests {

    private sealed class FixedTimeProvider(long timestamp, long frequency) : TimeProvider {
        public override long GetTimestamp() => timestamp;
        public override long TimestampFrequency => frequency;
    }

    private static long Expected(BigInteger value, long numerator, long denominator) {
        return (long)(value * numerator / denominator); // BigInteger division truncates toward zero
    }

    [Theory]
    [InlineData(9_007_199_254_740_993, 1_000_000_000)] // 2^53 + 1: a double drops the last tick
    [InlineData(4_611_686_018_427_387_903, 1_000_000_000)]
    [InlineData(-9_007_199_254_740_993, 1_000_000_000)]
    [InlineData(123_456_789, 1_000)]
    [InlineData(987_654_321_987, 3)]
    public void From_TimeProvider_ShouldScaleExactly(long rawTicks, long frequency) {
        MonotonicTimestamp timestamp = MonotonicTimestamp.From(new FixedTimeProvider(rawTicks, frequency));

        Assert.Equal(Expected(rawTicks, Stopwatch.Frequency, frequency), timestamp.RawTicks);
    }

    [Fact]
    public void From_TimeProvider_ShouldKeepTicks_WhenTheFrequencyMatches() {
        MonotonicTimestamp timestamp = MonotonicTimestamp.From(new FixedTimeProvider(long.MaxValue, Stopwatch.Frequency));

        Assert.Equal(long.MaxValue, timestamp.RawTicks);
    }

    [Fact]
    public void From_TimeProvider_ShouldThrow_WhenScalingOverflows() {
        Assert.Throws<OverflowException>(() => MonotonicTimestamp.From(new FixedTimeProvider(long.MaxValue, 1)));
        Assert.Throws<OverflowException>(() => MonotonicTimestamp.From(new FixedTimeProvider(long.MinValue, 1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000_000)]
    [InlineData(9_007_199_254_740_993)]
    [InlineData(-9_007_199_254_740_993)]
    public void Add_And_Subtract_ShouldScaleTimeSpanTicksExactly(long timeSpanTicks) {
        MonotonicTimestamp start = MonotonicTimestamp.FromRawTicks(0);
        long expected = Expected(timeSpanTicks, Stopwatch.Frequency, TimeSpan.TicksPerSecond);

        Assert.Equal(expected, (start + TimeSpan.FromTicks(timeSpanTicks)).RawTicks);
        Assert.Equal(-expected, (start - TimeSpan.FromTicks(timeSpanTicks)).RawTicks);
    }

    [Fact]
    public void Add_ShouldThrow_InsteadOfWrappingIntoThePast() {
        MonotonicTimestamp now = MonotonicTimestamp.FromRawTicks(1_000_000);

        Assert.Throws<OverflowException>(() => now + TimeSpan.MaxValue);
        Assert.Throws<OverflowException>(() => MonotonicTimestamp.MaxValue + TimeSpan.FromSeconds(1));
        Assert.Throws<OverflowException>(() => MonotonicTimestamp.MaxValue.AddSeconds(1));
    }

    [Fact]
    public void Subtract_ShouldThrow_InsteadOfWrappingIntoTheFuture() {
        MonotonicTimestamp lowest = MonotonicTimestamp.FromRawTicks(long.MinValue);

        Assert.Throws<OverflowException>(() => lowest - TimeSpan.FromSeconds(1));
        Assert.Throws<OverflowException>(() => MonotonicTimestamp.FromRawTicks(-1_000_000) + TimeSpan.MinValue);
        Assert.Throws<OverflowException>(() => MonotonicTimestamp.MaxValue.Subtract(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Subtract_ShouldAllowResultsBeforeZero() {
        // Shortly after boot, "now - window" is legitimately below zero and still compares correctly.
        MonotonicTimestamp now = MonotonicTimestamp.FromRawTicks(Stopwatch.Frequency);
        MonotonicTimestamp cutoff = now - TimeSpan.FromHours(1);

        Assert.True(cutoff.RawTicks < 0);
        Assert.True(cutoff < now);
        Assert.Equal(TimeSpan.FromHours(1), now - cutoff);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1_000, 123_456_789)]
    [InlineData(-9_007_199_254_740_993, 9_007_199_254_740_993)]
    [InlineData(5, -5)]
    public void Difference_ShouldScaleExactly(long startTicks, long endTicks) {
        MonotonicTimestamp start = MonotonicTimestamp.FromRawTicks(startTicks);
        MonotonicTimestamp end = MonotonicTimestamp.FromRawTicks(endTicks);
        TimeSpan expected = TimeSpan.FromTicks(Expected((BigInteger)endTicks - startTicks, TimeSpan.TicksPerSecond, Stopwatch.Frequency));

        Assert.Equal(expected, end - start);
        Assert.Equal(expected, start.ElapsedUntil(end));
    }

    [Fact]
    public void Difference_ShouldThrow_InsteadOfWrappingAround() {
        MonotonicTimestamp lowest = MonotonicTimestamp.FromRawTicks(long.MinValue);

        Assert.Throws<OverflowException>(() => MonotonicTimestamp.MaxValue - lowest);
        Assert.Throws<OverflowException>(() => lowest - MonotonicTimestamp.MaxValue);
        Assert.Throws<OverflowException>(() => lowest.ElapsedUntil(MonotonicTimestamp.MaxValue));
    }
}
