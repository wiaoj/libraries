namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangeSemVerExtensionsTests {
    [Fact]
    public void FilterCompatible_ReturnsOnlyVersionsInRange() {
        // Arrange
        SemVer minVersion = SemVer.Parse("1.0.0");
        SemVer maxVersion = SemVer.Parse("2.0.0");
        Range<SemVer> range = new(minVersion, maxVersion);

        List<SemVer> versions = new() {
            SemVer.Parse("0.9.0"), // Outside
            SemVer.Parse("1.0.0"), // Inside (Boundary)
            SemVer.Parse("1.5.0"), // Inside
            SemVer.Parse("2.0.0"), // Inside (Boundary)
            SemVer.Parse("2.1.0")  // Outside
        };

        // Act
        List<SemVer> compatible = range.FilterCompatible(versions).ToList();

        // Assert
        Assert.Equal(3, compatible.Count);
        Assert.Contains(SemVer.Parse("1.0.0"), compatible);
        Assert.Contains(SemVer.Parse("1.5.0"), compatible);
        Assert.Contains(SemVer.Parse("2.0.0"), compatible);
    }

    [Fact]
    public void GetLatestCompatible_ReturnsHighestVersionInRange() {
        // Arrange
        Range<SemVer> range = new(SemVer.Parse("1.0.0"), SemVer.Parse("2.5.0"));
        List<SemVer> versions = new() {
            SemVer.Parse("0.5.0"),
            SemVer.Parse("1.2.0"),
            SemVer.Parse("2.4.9"), // Expected Target
            SemVer.Parse("3.0.0")
        };

        // Act
        var latest = range.GetLatestCompatible(versions);

        // Assert
        Assert.NotNull(latest);
        Assert.Equal(SemVer.Parse("2.4.9"), latest);
    }

    [Fact]
    public void GetLatestCompatible_NoVersionsInRange_ReturnsNull() {
        // Arrange
        Range<SemVer> range = new(SemVer.Parse("1.0.0"), SemVer.Parse("2.0.0"));
        List<SemVer> versions = new() {
            SemVer.Parse("0.5.0"),
            SemVer.Parse("3.0.0")
        };

        // Act
        var latest = range.GetLatestCompatible(versions);

        // Assert
        Assert.Null(latest);
    }
}