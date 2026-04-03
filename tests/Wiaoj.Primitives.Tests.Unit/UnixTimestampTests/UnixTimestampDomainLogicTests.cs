namespace Wiaoj.Primitives.Tests.Unit.UnixTimestampTests;

public sealed class UnixTimestampDomainLogicTests {
    [Fact]
    public void IsBetween_ShouldReturnTrue_IfInclusiveAndWithinRange() {
        var start = UnixTimestamp.FromMilliseconds(100);
        var end = UnixTimestamp.FromMilliseconds(300);

        Assert.True(UnixTimestamp.FromMilliseconds(200).IsBetween(start, end));
        Assert.True(UnixTimestamp.FromMilliseconds(100).IsBetween(start, end));
        Assert.True(UnixTimestamp.FromMilliseconds(300).IsBetween(start, end));
        Assert.False(UnixTimestamp.FromMilliseconds(99).IsBetween(start, end));
        Assert.False(UnixTimestamp.FromMilliseconds(301).IsBetween(start, end));
    }

    [Fact]
    public void IsBetween_ShouldBeInclusive_OnExactBoundaries() {
        var start = UnixTimestamp.FromMilliseconds(5000);
        var end = UnixTimestamp.FromMilliseconds(10000);

        Assert.True(start.IsBetween(start, end), "Start boundary should be inclusive.");
        Assert.True(end.IsBetween(start, end), "End boundary should be inclusive.");
    }

    [Fact]
    public void IsBefore_And_IsAfter_ShouldWorkStrictly() {
        var baseTime = UnixTimestamp.FromMilliseconds(1000);
        var earlier = UnixTimestamp.FromMilliseconds(500);
        var later = UnixTimestamp.FromMilliseconds(1500);

        Assert.True(earlier.IsBefore(baseTime));
        Assert.False(baseTime.IsBefore(baseTime));

        Assert.True(later.IsAfter(baseTime));
        Assert.False(baseTime.IsAfter(baseTime));
    }

    [Fact]
    public void ElapsedSince_ShouldReturnTimeSpanDifference() {
        var past = UnixTimestamp.FromMilliseconds(1000);
        var current = UnixTimestamp.FromMilliseconds(2500);

        TimeSpan elapsed = current.ElapsedSince(past);
        Assert.Equal(1500, elapsed.TotalMilliseconds);
    }

    [Fact]
    public void TimeProvider_Factories_ShouldWork() {
        var fakeProvider = new TestHelpers.FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var ts = UnixTimestamp.From(fakeProvider);
        var tsExt = fakeProvider.GetUnixTimestamp();

        Assert.Equal(ts.TotalMilliseconds, tsExt.TotalMilliseconds);
        Assert.Equal(fakeProvider.CurrentUtc.ToUnixTimeMilliseconds(), ts.TotalMilliseconds);
    }

    [Fact]
    public void TimePassed_WithTimeProvider_ShouldReturnCorrectDuration() {
        var fakeProvider = new TestHelpers.FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var pastEvent = fakeProvider.GetUnixTimestamp().AddHours(-2);

        TimeSpan passed = pastEvent.TimePassed(fakeProvider);
        Assert.Equal(TimeSpan.FromHours(2), passed);
    }

    [Fact]
    public void TimeUntil_WithTimeProvider_ShouldReturnCorrectDuration() {
        var fakeProvider = new TestHelpers.FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var futureEvent = fakeProvider.GetUnixTimestamp().AddMinutes(30);

        TimeSpan remaining = futureEvent.TimeUntil(fakeProvider);
        Assert.Equal(TimeSpan.FromMinutes(30), remaining);
    }

    [Fact]
    public void IsOlderThan_WithTimeProvider_ShouldWorkProperly() {
        var fakeProvider = new TestHelpers.FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var twoHoursAgo = fakeProvider.GetUnixTimestamp().AddHours(-2);

        Assert.True(twoHoursAgo.IsOlderThan(TimeSpan.FromHours(1), fakeProvider));
        Assert.False(twoHoursAgo.IsOlderThan(TimeSpan.FromHours(3), fakeProvider));
    }

    [Fact]
    public void RealSystemTime_Methods_ShouldNotThrow() {
        var tsPast = UnixTimestamp.Now.AddDays(-1);
        Assert.True(tsPast.TimePassed() > TimeSpan.Zero);
        Assert.True(tsPast.IsOlderThan(TimeSpan.FromHours(12)));

        var tsFuture = UnixTimestamp.Now.AddDays(1);
        Assert.True(tsFuture.TimeUntil() > TimeSpan.Zero);
    }
}