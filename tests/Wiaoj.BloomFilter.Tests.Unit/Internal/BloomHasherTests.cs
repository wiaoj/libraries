using System.Text;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

public sealed class BloomHasherTests {
    public sealed class ComputeBaseHashesMethod {
        [Fact]
        public void Should_ProduceDeterministicHashes_ForIdenticalInputAndSeed() {
            // Arrange
            byte[] input = Encoding.UTF8.GetBytes("https://example.com/item/1");
            long seed = 42;

            // Act
            BloomHasher.ComputeBaseHashes(input, seed, out ulong firstH1, out ulong firstH2);
            BloomHasher.ComputeBaseHashes(input, seed, out ulong secondH1, out ulong secondH2);

            // Assert
            Assert.Equal(firstH1, secondH1);
            Assert.Equal(firstH2, secondH2);
            Assert.NotEqual(0UL, firstH1);
        }

        [Fact]
        public void Should_ProduceDifferentHashes_ForDifferentInputs() {
            // Arrange
            byte[] input1 = Encoding.UTF8.GetBytes("key-alpha");
            byte[] input2 = Encoding.UTF8.GetBytes("key-beta");
            long seed = 42;

            // Act
            BloomHasher.ComputeBaseHashes(input1, seed, out ulong h1A, out ulong h2A);
            BloomHasher.ComputeBaseHashes(input2, seed, out ulong h1B, out ulong h2B);

            // Assert
            Assert.NotEqual(h1A, h1B);
        }
    }

    public sealed class GetBitPositionMethod {
        [Fact]
        public void Should_GenerateBitPositions_WithinConfiguredBitSize() {
            // Arrange
            byte[] input = Encoding.UTF8.GetBytes("consistent-test-data");
            long sizeInBits = 100_000;
            int hashCount = 10;
            BloomHasher.ComputeBaseHashes(input, 12345, out ulong h1, out ulong h2);

            // Act & Assert
            for(int i = 0; i < hashCount; i++) {
                long pos = BloomHasher.GetBitPosition(h1, h2, i, sizeInBits);
                Assert.InRange(pos, 0, sizeInBits - 1);
            }
        }

        [Fact]
        public void Should_GenerateDistinctBitPositions_AcrossIterations() {
            // Arrange
            byte[] input = Encoding.UTF8.GetBytes("kirsch-mitzenmacher-test");
            long sizeInBits = 10_000_000;
            int hashCount = 8;
            BloomHasher.ComputeBaseHashes(input, 999, out ulong h1, out ulong h2);
            HashSet<long> generatedPositions = [];

            // Act
            for(int i = 0; i < hashCount; i++) {
                generatedPositions.Add(BloomHasher.GetBitPosition(h1, h2, i, sizeInBits));
            }

            // Assert
            Assert.Equal(hashCount, generatedPositions.Count);
        }
    }
}