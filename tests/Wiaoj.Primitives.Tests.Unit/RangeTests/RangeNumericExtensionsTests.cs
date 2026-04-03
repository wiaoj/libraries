namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangeNumericExtensionsTests {
    [Fact]
    public void Length_IntType_ReturnsCorrectDifference() {
        // Arrange
        Range<int> range = new(10, 25);

        // Act
        int length = range.Length();

        // Assert
        Assert.Equal(15, length);
    }

    [Fact]
    public void Length_DoubleType_ReturnsCorrectDifference() {
        // Arrange
        Range<double> range = new(1.5, 4.0);

        // Act
        double length = range.Length();

        // Assert
        Assert.Equal(2.5, length);
    }

    [Theory]
    [InlineData(10, 10)] // Inside (10 gönderilir, 10 çıkar)
    [InlineData(1, 5)]   // Below Min (1 gönderilir, Range Min'i olan 5 çıkar)
    [InlineData(20, 15)] // Above Max (20 gönderilir, Range Max'i olan 15 çıkar)
    public void Clamp_NumericValues_ReturnsClampedValue(int input, int expected) {
        // Arrange
        var range = new Range<int>(5, 15);

        // Act
        int result = range.Clamp(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Gap_OverlappingRanges_ReturnsNull() {
        // Arrange
        Range<int> r1 = new(0, 10);
        Range<int> r2 = new(5, 15);

        // Act
        Range<int>? gap = r1.Gap(r2);

        // Assert
        Assert.Null(gap);
    }

    [Fact]
    public void Gap_NonOverlappingRanges_ReturnsCorrectGapRange() {
        // Arrange
        Range<int> r1 = new(1, 5);
        Range<int> r2 = new(10, 15);

        // Act
        Range<int>? gap1 = r1.Gap(r2); // r1 before r2
        Range<int>? gap2 = r2.Gap(r1); // r2 before r1

        // Assert
        Assert.NotNull(gap1);
        Assert.Equal(5, gap1.Value.Min);
        Assert.Equal(10, gap1.Value.Max);

        Assert.NotNull(gap2);
        Assert.Equal(5, gap2.Value.Min);
        Assert.Equal(10, gap2.Value.Max);
    }
}