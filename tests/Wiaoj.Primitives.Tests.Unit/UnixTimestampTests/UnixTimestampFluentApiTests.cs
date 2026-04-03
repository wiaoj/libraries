namespace Wiaoj.Primitives.Tests.Unit.UnixTimestampTests;

public sealed class UnixTimestampFluentApiTests {
    [Fact]
    public void AddMilliseconds_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1000);
        Assert.Equal(1500, ts.AddMilliseconds(500).TotalMilliseconds);
        Assert.Equal(800, ts.AddMilliseconds(-200).TotalMilliseconds);
    }

    [Fact]
    public void AddSeconds_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1000);
        Assert.Equal(2000, ts.AddSeconds(1).TotalMilliseconds);
        Assert.Equal(1500, ts.AddSeconds(0.5).TotalMilliseconds);
    }

    [Fact]
    public void AddMinutes_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(0);
        Assert.Equal(60000, ts.AddMinutes(1).TotalMilliseconds);
    }

    [Fact]
    public void AddHours_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(0);
        Assert.Equal(3600000, ts.AddHours(1).TotalMilliseconds);
    }

    [Fact]
    public void AddDays_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(0);
        Assert.Equal(86400000, ts.AddDays(1).TotalMilliseconds);
        Assert.Equal(129600000, ts.AddDays(1.5).TotalMilliseconds);
    }

    [Fact]
    public void TruncateToSeconds_ShouldZeroOutMilliseconds() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1234567);
        Assert.Equal(1234000, ts.TruncateToSeconds().TotalMilliseconds);
    }

    [Fact]
    public void TruncateToMinutes_ShouldZeroOutSecondsAndMilliseconds() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(65500);
        Assert.Equal(60000, ts.TruncateToMinutes().TotalMilliseconds);
    }

    [Fact]
    public void TruncateToDay_ShouldRoundToMidnightUtc() {
        DateTimeOffset dto = new(2024, 2, 11, 15, 30, 45, 123, TimeSpan.Zero);
        DateTimeOffset expectedMidnightDto = new(2024, 2, 11, 0, 0, 0, TimeSpan.Zero);

        UnixTimestamp ts = dto.ToUnixTimestamp();
        var truncated = ts.TruncateToDay();

        Assert.Equal(expectedMidnightDto.ToUnixTimeMilliseconds(), truncated.TotalMilliseconds);
    }

    [Fact]
    public void TruncateToDay_NegativeTimestamps_ShouldWorkCorrectly() {
        DateTimeOffset dto = new(1969, 12, 31, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset expectedMidnightDto = new(1969, 12, 31, 0, 0, 0, TimeSpan.Zero);

        UnixTimestamp ts = dto.ToUnixTimestamp();
        var truncated = ts.TruncateToDay();

        Assert.Equal(expectedMidnightDto.ToUnixTimeMilliseconds(), truncated.TotalMilliseconds);
    }

    [Fact]
    public void TruncateToDay_BeforeEpoch_ShouldRoundToMidnightUtc() {
        // 31 Aralık 1969, 12:00:00 UTC (-43,200,000 ms)
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(-43200000);
        // Beklenen: 31 Aralık 1969, 00:00:00 UTC (-86,400,000 ms)
        long expected = -86400000;

        var truncated = ts.TruncateToDay();

        Assert.Equal(expected, truncated.TotalMilliseconds);
    }
}