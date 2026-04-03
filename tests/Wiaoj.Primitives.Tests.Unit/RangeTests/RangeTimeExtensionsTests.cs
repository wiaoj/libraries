namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangeTimeExtensionsTests {
    [Fact]
    public void Duration_DateTime_ReturnsCorrectTimeSpan() {
        // Arrange
        DateTime min = new(2024, 1, 1, 10, 0, 0);
        DateTime max = new(2024, 1, 1, 12, 30, 0);
        Range<DateTime> range = new(min, max);

        // Act
        var duration = range.Duration();

        // Assert
        Assert.Equal(TimeSpan.FromHours(2.5), duration);
    }

    [Fact]
    public void DurationDays_DateOnly_ReturnsCorrectDayCount() {
        // Arrange
        DateOnly min = new(2024, 1, 1);
        DateOnly max = new(2024, 1, 10);
        Range<DateOnly> range = new(min, max);

        // Act
        int days = range.DurationDays();

        // Assert
        Assert.Equal(9, days);
    }

    [Fact]
    public void Duration_TimeOnly_ReturnsCorrectTimeSpan() {
        var min = new TimeOnly(10, 0, 0);
        var max = new TimeOnly(12, 30, 0);
        var range = new Range<TimeOnly>(min, max);

        Assert.Equal(TimeSpan.FromHours(2.5), range.Duration());
    }

    [Fact]
    public void Duration_UnixTimestamp_ReturnsCorrectTimeSpan() {
        // UnixTimestamp sınıfınızın FromMilliseconds metodunu kullandığını varsayarak:
        var min = UnixTimestamp.FromMilliseconds(10000); // 10 saniye
        var max = UnixTimestamp.FromMilliseconds(25000); // 25 saniye
        var range = new Range<UnixTimestamp>(min, max);

        Assert.Equal(TimeSpan.FromSeconds(15), range.Duration());
    }

    [Fact]
    public void IsPast_DateTimeOffset_MaxIsBeforeNow_ReturnsTrue() {
        var max = DateTimeOffset.UtcNow.AddMinutes(-5);
        var range = new Range<DateTimeOffset>(max.AddMinutes(-10), max);

        Assert.True(range.IsPast());
    }

    [Fact]
    public void IsPast_DateTime_MaxIsBeforeNow_ReturnsTrue() {
        // Arrange
        var max = DateTime.UtcNow.AddMinutes(-5);
        Range<DateTime> range = new(max.AddMinutes(-10), max);

        // Act
        bool isPast = range.IsPast();

        // Assert
        Assert.True(isPast);
    }

    [Fact]
    public void IsFuture_DateTime_MinIsAfterNow_ReturnsTrue() {
        // Arrange
        var min = DateTime.UtcNow.AddMinutes(5);
        Range<DateTime> range = new(min, min.AddMinutes(10));

        // Act
        bool isFuture = range.IsFuture();

        // Assert
        Assert.True(isFuture);
    }

    [Fact]
    public void IsNowWithin_DateTime_NowIsInRange_ReturnsTrue() {
        // Arrange
        var min = DateTime.UtcNow.AddMinutes(-5);
        var max = DateTime.UtcNow.AddMinutes(5);
        Range<DateTime> range = new(min, max);

        // Act
        bool isNowWithin = range.IsNowWithin();

        // Assert
        Assert.True(isNowWithin);
    }

    [Fact]
    public void IsTodayWithin_DateOnly_TodayIsInRange_ReturnsTrue() {
        // Arrange
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        Range<DateOnly> range = new(today.AddDays(-1), today.AddDays(1));

        // Act
        bool isTodayWithin = range.IsTodayWithin();

        // Assert
        Assert.True(isTodayWithin);
    }
}