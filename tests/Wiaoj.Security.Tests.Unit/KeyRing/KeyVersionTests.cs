namespace Wiaoj.Security.Tests.Unit.KeyRing;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyRing")]
public class KeyVersionTests {

    [Theory]
    [InlineData(0, "v0")]
    [InlineData(1, "v1")]
    [InlineData(100, "v100")]
    public void Of_WithNonNegativeInteger_ShouldCreateValidKeyVersion(int value, string expectedString) {
        // Act
        KeyVersion version = KeyVersion.Of(value);

        // Assert
        Assert.Equal(value, version.Value);
        Assert.Equal(expectedString, version.ToString());
    }

    [Fact]
    public void Of_WithNegativeInteger_ShouldThrowArgumentOutOfRangeException() {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyVersion.Of(-1));
    }

    [Fact]
    public void ImplicitAndExplicitCasts_ShouldWorkAsExpected() {
        // Explicit int -> KeyVersion
        KeyVersion version = (KeyVersion)5;
        Assert.Equal(5, version.Value);

        // Implicit KeyVersion -> int
        int value = version;
        Assert.Equal(5, value);
    }

    [Theory]
    [InlineData(1, 2, true, false, true, false)]
    [InlineData(2, 1, false, true, false, true)]
    [InlineData(3, 3, false, false, true, true)]
    public void ComparisonOperators_ShouldEvaluateCorrectly(
        int v1, int v2, bool isLess, bool isGreater, bool isLessOrEqual, bool isGreaterOrEqual) {
        // Arrange
        KeyVersion left = KeyVersion.Of(v1);
        KeyVersion right = KeyVersion.Of(v2);

        // Assert
        Assert.Equal(isLess, left < right);
        Assert.Equal(isGreater, left > right);
        Assert.Equal(isLessOrEqual, left <= right);
        Assert.Equal(isGreaterOrEqual, left >= right);
        Assert.Equal(v1.CompareTo(v2), left.CompareTo(right));
    }
}