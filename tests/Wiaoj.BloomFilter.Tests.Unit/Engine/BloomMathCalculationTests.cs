using Wiaoj.BloomFilter.Engine;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public sealed class BloomMathCalculationTests {
    public sealed class EstimateInsertedItemsMethod {
        [Fact]
        public void Should_ReturnZero_When_SetBitsCountIsZeroOrNegative() {
            // Arrange
            long sizeInBits = 100_000;
            int hashFunctions = 7;

            // Act
            long resultZero = BloomMath.EstimateInsertedItems(sizeInBits, 0, hashFunctions);
            long resultNegative = BloomMath.EstimateInsertedItems(sizeInBits, -5, hashFunctions);

            // Assert
            Assert.Equal(0, resultZero);
            Assert.Equal(0, resultNegative);
        }

        [Fact]
        public void Should_ReturnSizeInBits_When_SetBitsCountReachesOrEqualToCapacity() {
            // Arrange
            long sizeInBits = 100_000;
            int hashFunctions = 7;

            // Act
            long resultExact = BloomMath.EstimateInsertedItems(sizeInBits, 100_000, hashFunctions);
            long resultOverflow = BloomMath.EstimateInsertedItems(sizeInBits, 120_000, hashFunctions);

            // Assert
            Assert.Equal(sizeInBits, resultExact);
            Assert.Equal(sizeInBits, resultOverflow);
        }
    }

    public sealed class EstimateFalsePositiveProbabilityMethod {
        [Fact]
        public void Should_CalculateAccurately_BasedOnSaturationAndHashCount() {
            // Arrange
            Percentage fillRatio = Percentage.FromDouble(0.50);
            int hashCount = 4;

            // Act
            Percentage estimatedFp = BloomMath.EstimateFalsePositiveProbability(fillRatio, hashCount);

            // Assert: (0.50)^4 = 0.0625 (6.25%)
            Assert.Equal(0.0625, estimatedFp.Value, precision: 4);
        }
    }

    public sealed class CalculateOptimalShardCountMethod {
        [Theory]
        [InlineData(1_000, 10_000, 1)]      // Below threshold -> 1 shard
        [InlineData(100_000, 10_000, 16)]   // 100KB / 10KB = 10 -> rounded up to power of 2 -> 16 shards
        [InlineData(25_000, 10_000, 4)]     // 25KB / 10KB = 2.5 -> rounded up to power of 2 -> 4 shards
        public void Should_ReturnCorrectPowerOfTwo_When_ThresholdIsSpecified(long totalBytes, long thresholdBytes, int expectedShards) {
            // Arrange
            long sizeInBits = totalBytes * 8;

            // Act
            int actualShards = BloomMath.CalculateOptimalShardCount(sizeInBits, thresholdBytes);

            // Assert
            Assert.Equal(expectedShards, actualShards);
        }
    }
}