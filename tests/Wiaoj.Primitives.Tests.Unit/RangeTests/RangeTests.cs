using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangeTests {
    [Fact]
    public void Constructor_ValidMinMax_SetsPropertiesCorrectly() {
        // Arrange
        int min = 5;
        int max = 10;

        // Act
        Range<int> range = new(min, max);

        // Assert
        Assert.Equal(min, range.Min);
        Assert.Equal(max, range.Max);
    }

    [Fact]
    public void Constructor_MinEqualsMax_CreatesSinglePointRange() {
        // Arrange
        int val = 5;

        // Act
        Range<int> range = new(val, val);

        // Assert
        Assert.Equal(val, range.Min);
        Assert.Equal(val, range.Max);
    }

    [Fact]
    public void Constructor_MinGreaterThanMax_ThrowsPrecaArgumentException() {
        // Arrange & Act & Assert
        // PrecaArgumentException kullandığınızı varsayıyorum, ArgumentException da olabilir.
        Assert.ThrowsAny<PrecaArgumentException>(() => new Range<int>(10, 5));
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(5.0, double.NaN)]
    public void Constructor_DoubleNaN_ThrowsArgumentException(double min, double max) {
        // Arrange & Act & Assert
        Assert.ThrowsAny<PrecaArgumentOutOfRangeException>(() => new Range<double>(min, max));
    }

    [Theory]
    [InlineData(float.NaN, 10f)]
    [InlineData(5f, float.NaN)]
    public void Constructor_FloatNaN_ThrowsArgumentException(float min, float max) {
        Assert.ThrowsAny<PrecaArgumentOutOfRangeException>(() => new Range<float>(min, max));
    }

    [Fact]
    public void Constructor_HalfNaN_ThrowsArgumentException() {
        Assert.ThrowsAny<PrecaArgumentOutOfRangeException>(() => new Range<Half>(Half.NaN, (Half)10));
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException() {
        // Arrange
        string min = null!;
        string max = "Z";

        // Act & Assert
        Assert.Throws<PrecaArgumentNullException>(() => new Range<string>(min, max));
    }

    [Fact]
    public void Constructor_FromSystemRange_ValidIntRange_CreatesCorrectRange() {
        // Arrange
        System.Range sysRange = 10..50;

        // Act
        Range<int> range = new(sysRange);

        // Assert
        Assert.Equal(10, range.Min);
        Assert.Equal(50, range.Max);
    }

    [Fact]
    public void Constructor_FromSystemRange_ReversedIntRange_AutoSwapsBounds() {
        // Arrange
        System.Range sysRange = 50..10;

        // Act
        Range<int> range = new(sysRange);

        // Assert
        Assert.Equal(10, range.Min);
        Assert.Equal(50, range.Max);
    }

    [Fact]
    public void Constructor_FromSystemRange_FromEndIndex_ThrowsArgumentException() {
        // Arrange
        System.Range sysRange = ^5..^1;

        // Act & Assert
        Assert.Throws<PrecaArgumentException>(() => new Range<int>(sysRange));
    }

    [Fact]
    public void Constructor_FromSystemRange_NonIntType_ThrowsInvalidOperationException() {
        // Arrange
        System.Range sysRange = 1..5;

        // Act & Assert
        Assert.Throws<PrecaInvalidOperationException>(() => new Range<double>(sysRange));
    }

    [Fact]
    public void Create_FirstValGreaterThanSecond_AutoSwaps() {
        // Arrange & Act
        Range<int> range = Range<int>.Create(100, 50);

        // Assert
        Assert.Equal(50, range.Min);
        Assert.Equal(100, range.Max);
    }

    [Fact]
    public void Between_WorksExactlyLikeCreate() {
        // Arrange & Act
        Range<DateTime> range = Range<DateTime>.Between(new DateTime(2025, 1, 1), new DateTime(2024, 1, 1));

        // Assert
        Assert.Equal(new DateTime(2024, 1, 1), range.Min);
        Assert.Equal(new DateTime(2025, 1, 1), range.Max);
    }

    [Theory]
    [InlineData(5, true)]  // Boundary Min
    [InlineData(10, true)] // Boundary Max
    [InlineData(7, true)]  // Inside
    [InlineData(4, false)] // Outside Below
    [InlineData(11, false)]// Outside Above
    public void Contains_Value_ReturnsExpectedResult(int value, bool expected) {
        // Arrange
        Range<int> range = new(5, 10);

        // Act
        bool result = range.Contains(value);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Contains_OtherRange_ReturnsTrueIfFullyInside() {
        // Arrange
        Range<int> outer = new(0, 100);
        Range<int> inner = new(20, 80);

        // Act & Assert
        Assert.True(outer.Contains(inner));
    }

    [Fact]
    public void Contains_OtherRange_ReturnsFalseIfPartialOrOutside() {
        // Arrange
        Range<int> outer = new(10, 20);
        Range<int> partial = new(5, 15);
        Range<int> outside = new(30, 40);

        // Act & Assert
        Assert.False(outer.Contains(partial));
        Assert.False(outer.Contains(outside));
    }

    [Fact]
    public void Overlaps_ReturnsCorrectResult() {
        // Arrange
        Range<int> baseRange = new(10, 20);

        Range<int> overlappingStart = new(5, 15);
        Range<int> overlappingEnd = new(15, 25);
        Range<int> exactMatch = new(10, 20);
        Range<int> noOverlapBefore = new(0, 9);
        Range<int> noOverlapAfter = new(21, 30);

        // Act & Assert
        Assert.True(baseRange.Overlaps(overlappingStart));
        Assert.True(baseRange.Overlaps(overlappingEnd));
        Assert.True(baseRange.Overlaps(exactMatch));
        Assert.False(baseRange.Overlaps(noOverlapBefore));
        Assert.False(baseRange.Overlaps(noOverlapAfter));
    }

    [Fact]
    public void Intersect_OverlappingRanges_ReturnsIntersection() {
        // Arrange
        Range<int> r1 = new(0, 10);
        Range<int> r2 = new(5, 15);

        // Act
        Range<int>? intersection = r1.Intersect(r2);

        // Assert
        Assert.NotNull(intersection);
        Assert.Equal(5, intersection.Value.Min);
        Assert.Equal(10, intersection.Value.Max);
    }

    [Fact]
    public void Intersect_NonOverlappingRanges_ReturnsNull() {
        // Arrange
        Range<int> r1 = new(0, 5);
        Range<int> r2 = new(10, 15);

        // Act
        Range<int>? intersection = r1.Intersect(r2);

        // Assert
        Assert.Null(intersection);
    }

    [Fact]
    public void Union_ReturnsBoundingRange() {
        // Arrange
        Range<int> r1 = new(5, 10);
        Range<int> r2 = new(20, 25);

        // Act
        Range<int> union = r1.Union(r2);

        // Assert
        Assert.Equal(5, union.Min);
        Assert.Equal(25, union.Max);
    }

    [Fact]
    public void ToString_FormatsCorrectly() {
        // Arrange
        Range<int> range = new(1, 5);

        // Act
        string str = range.ToString();

        // Assert
        Assert.Equal("[1, 5]", str);
    }

    [Fact]
    public void Deconstruct_WorksProperly() {
        // Arrange
        Range<int> range = new(7, 14);

        // Act
        (int min, int max) = range;

        // Assert
        Assert.Equal(7, min);
        Assert.Equal(14, max);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        // Arrange
        Range<int> r1 = new(1, 10);
        Range<int> r2 = new(1, 10);

        // Act & Assert
        Assert.True(r1 == r2);
        Assert.Equal(r1, r2);
        Assert.Equal(r1.GetHashCode(), r2.GetHashCode());
    }
}