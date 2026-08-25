using Wiaoj.DistributedCounter;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "CounterExpiry")]
public sealed class CounterExpiryTests {

    [Fact]
    public void Infinite_HasNullValue_AndZeroTtlMilliseconds() {
        // Arrange & Act
        CounterExpiry infinite = CounterExpiry.Infinite;

        // Assert
        Assert.Null(infinite.Value);
        Assert.Equal(0, infinite.GetTtlMilliseconds());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void From_ZeroOrNegativeTimeSpan_ThrowsArgumentOutOfRangeException(long ticks) {
        // Arrange
        TimeSpan invalidSpan = TimeSpan.FromTicks(ticks);

        // Act & Assert
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => CounterExpiry.From(invalidSpan));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(86400)]
    public void FromSeconds_ValidInput_CalculatesCorrectTtl(double seconds) {
        // Act
        CounterExpiry expiry = CounterExpiry.FromSeconds(seconds);

        // Assert
        Assert.NotNull(expiry.Value);
        Assert.Equal(TimeSpan.FromSeconds(seconds), expiry.Value.Value);
        Assert.Equal((long)(seconds * 1000), expiry.GetTtlMilliseconds());
    }

    [Fact]
    public void ImplicitConversion_FromTimeSpan_ConstructsProperly() {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(5);

        // Act
        CounterExpiry expiry = span;

        // Assert
        Assert.Equal(span, expiry.Value);
        Assert.Equal(300_000, expiry.GetTtlMilliseconds());
    }

    [Fact]
    public void SubMillisecondPrecision_GetTtlMilliseconds_TruncatesOrRoundsSafely() {
        // 500 microseconds = 0.5 milliseconds
        CounterExpiry expiry = CounterExpiry.FromTicks(5000);

        // TotalMilliseconds is 0.5. (long)0.5 => 0
        long ttlMs = expiry.GetTtlMilliseconds();

        Assert.Equal(0, ttlMs);
    }
}