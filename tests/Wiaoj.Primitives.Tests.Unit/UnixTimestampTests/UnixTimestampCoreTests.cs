namespace Wiaoj.Primitives.Tests.Unit.UnixTimestampTests;

public sealed class UnixTimestampCoreTests {

    [Fact]
    public void Epoch_ShouldBeZero() {
        Assert.Equal(0, UnixTimestamp.Epoch.TotalMilliseconds);
    }

    [Fact]
    public void MinMaxValues_ShouldMatchDateTimeOffsetLimits() {
        Assert.Equal(DateTimeOffset.MinValue.ToUnixTimeMilliseconds(), UnixTimestamp.MinValue.TotalMilliseconds);
        Assert.Equal(DateTimeOffset.MaxValue.ToUnixTimeMilliseconds(), UnixTimestamp.MaxValue.TotalMilliseconds);
    }

    [Fact]
    public void TotalSeconds_ShouldReturnCorrectValue() {
        var ts = UnixTimestamp.FromMilliseconds(1500);
        Assert.Equal(1, ts.TotalSeconds);
    }

    [Fact]
    public void From_Long_ShouldStoreValue() {
        long val = 123456789;
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(val);
        Assert.Equal(val, ts.TotalMilliseconds);
    }

    [Fact]
    public void From_Seconds_ShouldMultiplyCorrectly() {
        UnixTimestamp ts = UnixTimestamp.FromSeconds(10);
        Assert.Equal(10000, ts.TotalMilliseconds);
    }

    [Fact]
    public void ImplicitOperator_ToLong_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(100);
        long val = ts;
        Assert.Equal(100, val);
    }

    [Fact]
    public void ExplicitOperator_FromLong_ShouldWork() {
        long val = 500;
        UnixTimestamp ts = (UnixTimestamp)val;
        Assert.Equal(500, ts.TotalMilliseconds);
    }

    [Fact]
    public void Now_ShouldBeCloseToSystemTime() {
        long systemMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UnixTimestamp now = UnixTimestamp.Now;
        long diff = Math.Abs(now.TotalMilliseconds - systemMs);
        Assert.True(diff < 500, "UnixTimestamp.Now is too far from system time.");
    }

    [Fact]
    public void Ticks_ShouldMatchDotNetDateTimeTicks() {
        var dto = new DateTimeOffset(2024, 2, 11, 10, 0, 0, TimeSpan.Zero);
        var ts = UnixTimestamp.From(dto);
        Assert.Equal(dto.Ticks, ts.Ticks);
    }

    [Fact]
    public void UnixTicks_ShouldMatchTicksSince1970() {
        var ts = UnixTimestamp.FromMilliseconds(1000);
        Assert.Equal(10_000_000, ts.UnixTicks);
    }

    [Fact]
    public void Operator_Plus_TimeSpan_ShouldAdd() {
        UnixTimestamp start = UnixTimestamp.FromMilliseconds(1000);
        UnixTimestamp result = start + TimeSpan.FromMilliseconds(500);
        Assert.Equal(1500, result.TotalMilliseconds);
    }

    [Fact]
    public void Operator_Minus_TimeSpan_ShouldSubtract() {
        UnixTimestamp start = UnixTimestamp.FromMilliseconds(1000);
        UnixTimestamp result = start - TimeSpan.FromMilliseconds(500);
        Assert.Equal(500, result.TotalMilliseconds);
    }

    [Fact]
    public void Operator_Minus_UnixTimestamp_ShouldReturnTimeSpan() {
        UnixTimestamp end = UnixTimestamp.FromMilliseconds(1500);
        UnixTimestamp start = UnixTimestamp.FromMilliseconds(1000);
        TimeSpan diff = end - start;
        Assert.Equal(500, diff.TotalMilliseconds);
    }

    [Fact]
    public void Comparison_Operators_ShouldWork() {
        UnixTimestamp small = UnixTimestamp.FromMilliseconds(100);
        UnixTimestamp big = UnixTimestamp.FromMilliseconds(200);
        Assert.True(small < big);
        Assert.True(big > small);
        Assert.True(small <= (UnixTimestamp)100);
        Assert.True(big >= (UnixTimestamp)200);
    }

    [Fact]
    public void Equality_With_UnixTimestamp() {
        UnixTimestamp t1 = (UnixTimestamp)123;
        UnixTimestamp t2 = (UnixTimestamp)123;
        Assert.True(t1 == t2);
        Assert.False(t1 != t2);
        Assert.True(t1.Equals(t2));
    }

    [Fact]
    public void Equality_With_Long() {
        UnixTimestamp t1 = (UnixTimestamp)1000;
        Assert.Equal(1000L, t1);
        Assert.NotEqual(2000L, t1);
    }

    [Fact]
    public void CompareTo_ShouldSortCorrectly() {
        List<UnixTimestamp> list = [(UnixTimestamp)300, (UnixTimestamp)100, (UnixTimestamp)200];
        list.Sort();
        Assert.Equal(100, list[0].TotalMilliseconds);
        Assert.Equal(200, list[1].TotalMilliseconds);
        Assert.Equal(300, list[2].TotalMilliseconds);
    }

    [Fact]
    public void GetHashCode_ShouldBeEqualForSameValue() {
        Assert.Equal(((UnixTimestamp)100).GetHashCode(), ((UnixTimestamp)100).GetHashCode());
    }
}