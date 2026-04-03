namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangePercentageExtensionsTests {
    [Fact]
    public void Length_PercentageRange_ReturnsCorrectDifference() {
        Range<Percentage> range = new(Percentage.FromDouble(0.2), Percentage.FromDouble(0.8));
        var length = range.Length();
        Assert.Equal(Percentage.FromDouble(0.6), length, precision: 10);
    }

    [Theory]
    [InlineData(0.5, 0.5)] // Inside
    [InlineData(0.1, 0.2)] // Below Min -> Returns Min (0.2)
    [InlineData(0.9, 0.8)] // Above Max -> Returns Max (0.8)
    public void Clamp_Percentage_ReturnsClampedValue(double inputVal, double expectedVal) {
        Range<Percentage> range = new(Percentage.FromDouble(0.2), Percentage.FromDouble(0.8));
        Percentage input = Percentage.FromDouble(inputVal);
        Percentage expected = Percentage.FromDouble(expectedVal);

        var result = range.Clamp(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Gap_Percentage_ReturnsCorrectGap() {
        Range<Percentage> r1 = new(Percentage.FromDouble(0.1), Percentage.FromDouble(0.3));
        Range<Percentage> r2 = new(Percentage.FromDouble(0.6), Percentage.FromDouble(0.9));

        var gap = r1.Gap(r2);

        Assert.NotNull(gap);
        Assert.Equal(Percentage.FromDouble(0.3), gap.Value.Min);
        Assert.Equal(Percentage.FromDouble(0.6), gap.Value.Max);
    }
}