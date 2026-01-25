using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit; 
public sealed class ConfigurationTests {
    [Theory]
    [InlineData(1000, 0.01, 9586, 7)]     // n=1000, p=1% -> m=9586, k=7
    [InlineData(1000000, 0.01, 9585059, 7)] // n=1M, p=1% -> m=9.5M, k=7
    public void Constructor_ShouldCalculateOptimalValues(long expectedItems, double errorRate, long expectedM, int expectedK) {
        BloomFilterConfiguration config = new("test", expectedItems, Percentage.FromDouble(errorRate));

        Assert.Equal(expectedM, config.SizeInBits);
        Assert.Equal(expectedK, config.HashFunctionCount);
    }

    [Fact]
    public void Constructor_InvalidInputs_ShouldThrow() {
        Assert.ThrowsAny<ArgumentException>(() => new BloomFilterConfiguration("test", -1, Percentage.FromDouble(0.01)));
        Assert.ThrowsAny<ArgumentException>(() => new BloomFilterConfiguration("test", 1000, Percentage.FromDouble(1.5)));
    }
}