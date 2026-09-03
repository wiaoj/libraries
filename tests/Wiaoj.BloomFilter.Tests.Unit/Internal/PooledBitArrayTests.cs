using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

public sealed class PooledBitArrayTests {
    public sealed class SetAndGetMethods {
        [Fact]
        public void Should_ReturnTrue_When_SettingUnsetBit() {
            // Arrange
            using PooledBitArray bitArray = new(1024);

            // Act
            bool changed = bitArray.Set(42);

            // Assert
            Assert.True(changed);
            Assert.True(bitArray.Get(42));
        }

        [Fact]
        public void Should_ReturnFalse_When_BitWasAlreadySet() {
            // Arrange
            using PooledBitArray bitArray = new(1024);
            bitArray.Set(42);

            // Act
            bool changedAgain = bitArray.Set(42);

            // Assert
            Assert.False(changedAgain);
            Assert.True(bitArray.Get(42));
        }

        [Fact]
        public void Should_MaintainIndependentBits_AcrossWordBoundaries() {
            // Arrange: spanning 64-bit boundaries (indices 0, 63, 64, 65, 127, 128, 255)
            using PooledBitArray bitArray = new(256);
            long[] testIndices = [0, 63, 64, 65, 127, 128, 255];

            // Act
            foreach(long idx in testIndices) {
                bitArray.Set(idx);
            }

            // Assert
            for(long i = 0; i < bitArray.Length; i++) {
                if(testIndices.Contains(i)) {
                    Assert.True(bitArray.Get(i), $"Bit at index {i} should be set");
                }
                else {
                    Assert.False(bitArray.Get(i), $"Bit at index {i} should NOT be set");
                }
            }
        }
    }

    public sealed class PopCountCalculation {
        [Fact]
        public void Should_AccuratelyCountSetBits() {
            // Arrange
            using PooledBitArray bitArray = new(1_000);
            long[] indicesToSet = [1, 5, 63, 64, 128, 500, 999];

            // Act
            foreach(long index in indicesToSet) {
                bitArray.Set(index);
            }

            // Assert
            Assert.Equal(indicesToSet.Length, bitArray.GetPopCount());
        }

        [Fact]
        public void Should_ReturnZero_ForNewlyInitializedArray() {
            // Arrange
            using PooledBitArray bitArray = new(4096);

            // Act
            long popCount = bitArray.GetPopCount();

            // Assert
            Assert.Equal(0, popCount);
        }
    }

    public sealed class StreamSerialization {
        [Fact]
        public async Task Should_RoundTripStateAccurately_ViaStream() {
            // Arrange
            using PooledBitArray source = new(10_000);
            source.Set(10);
            source.Set(64);
            source.Set(1024);
            source.Set(9999);

            using MemoryStream stream = new();

            // Act
            await source.WriteToStreamAsync(stream, CancellationToken.None);
            stream.Position = 0;

            using PooledBitArray destination = new(10_000);
            ulong loadedChecksum = await destination.LoadFromStreamAsync(stream, CancellationToken.None);

            // Assert
            Assert.Equal(source.GetPopCount(), destination.GetPopCount());
            Assert.True(destination.Get(10));
            Assert.True(destination.Get(64));
            Assert.True(destination.Get(1024));
            Assert.True(destination.Get(9999));
            Assert.False(destination.Get(11));
            Assert.Equal(source.CalculateChecksum(), loadedChecksum);
        }
    }

    public sealed class ChecksumCalculation {
        [Fact]
        public void Should_ProduceDifferentChecksum_WhenDataChanges() {
            // Arrange
            using PooledBitArray bits = new(1000);
            ulong initialChecksum = bits.CalculateChecksum();

            // Act
            bits.Set(100);
            ulong updatedChecksum = bits.CalculateChecksum();

            // Assert
            Assert.NotEqual(initialChecksum, updatedChecksum);
        }
    }
}