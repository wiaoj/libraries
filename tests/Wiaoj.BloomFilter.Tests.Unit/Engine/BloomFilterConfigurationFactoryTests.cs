using Wiaoj.BloomFilter.Engine;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterConfigurationFactoryTests {
    private readonly BloomFilterConfigurationFactory _sut = new();

    public sealed class CreateMethod : BloomFilterConfigurationFactoryTests {
        [Theory]
        [InlineData(1_000, 0.01, 9_586, 7)]
        [InlineData(10_000, 0.01, 95_851, 7)]
        [InlineData(100_000, 0.001, 1_437_759, 10)]
        public void Should_CalculateOptimalBitSizeAndHashCount_When_ParametersAreValid(
            long expectedItems,
            double errorRate,
            long expectedMinBits,
            int expectedHashCount) {
            // Arrange
            FilterName name = FilterName.Parse("test-filter");

            // Act
            BloomFilterConfiguration config = this._sut.Create(name, expectedItems, errorRate);

            // Assert
            Assert.Equal(name, config.Name);
            Assert.Equal(expectedItems, config.ExpectedItems);
            Assert.Equal(errorRate, config.ErrorRate);
            Assert.Equal(expectedMinBits, config.SizeInBits);
            Assert.Equal(expectedHashCount, config.HashFunctionCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void Should_ThrowArgumentException_When_ExpectedItemsIsZeroOrNegative(long invalidItems) {
            // Arrange
            FilterName name = FilterName.Parse("test-filter");

            // Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => this._sut.Create(name, invalidItems, 0.01));
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(1.5)]
        public void Should_ThrowArgumentException_When_ErrorRateIsOutOfRange(double invalidErrorRate) {
            // Arrange
            FilterName name = FilterName.Parse("test-filter");

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => this._sut.Create(name, 1_000, invalidErrorRate));
        }

        [Fact]
        public void Should_UseCustomSeed_When_Provided() {
            // Arrange
            FilterName name = FilterName.Parse("test-filter");
            long customSeed = 0x123456789ABCDEF;

            // Act
            BloomFilterConfiguration config = this._sut.Create(name, 1_000, 0.01, customSeed);

            // Assert
            Assert.Equal(customSeed, config.HashSeed);
        }
    }
}