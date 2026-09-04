
using Wiaoj.BloomFilter.Engine;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

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

        [Fact]
        public async Task Should_RoundTripStateAccurately_ViaSynchronousWriteToStream() {
            // Arrange
            using PooledBitArray source = new(10_000);
            source.Set(7);
            source.Set(63);
            source.Set(64);
            source.Set(5_000);

            using MemoryStream stream = new();

            // Act: Synchronous write
            source.WriteToStream(stream);
            stream.Position = 0;

            using PooledBitArray destination = new(10_000);
            ulong loadedChecksum = await destination.LoadFromStreamAsync(stream, CancellationToken.None);

            // Assert
            Assert.Equal(source.GetPopCount(), destination.GetPopCount());
            Assert.True(destination.Get(7));
            Assert.True(destination.Get(63));
            Assert.True(destination.Get(64));
            Assert.True(destination.Get(5_000));
            Assert.Equal(source.CalculateChecksum(), loadedChecksum);
        }

        [Fact]
        public async Task Should_HandleTruncatedStream_GracefullyWhenLoading() {
            // Arrange: Stream with fewer bytes than required
            using MemoryStream shortStream = new([0x01, 0x02, 0x03]);
            using PooledBitArray destination = new(10_000);

            // Act: Loading should calculate checksum over whatever was read without hanging
            ulong checksum = await destination.LoadFromStreamAsync(shortStream, CancellationToken.None);

            // Assert
            Assert.NotEqual(0UL, checksum);
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

    public sealed class BoundsAndExceptions {
        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(1024)]
        [InlineData(2000)]
        public void Should_ThrowException_When_AccessingOutOfBoundsIndices(long outOfBoundsIndex) {
            // Arrange
            using PooledBitArray bitArray = new(1024);

            // Act & Assert
            Assert.ThrowsAny<IndexOutOfRangeException>(() => bitArray.Get(outOfBoundsIndex));
            Assert.ThrowsAny<IndexOutOfRangeException>(() => bitArray.Set(outOfBoundsIndex));
        }

        [Fact]
        public void Should_ThrowException_When_AccessingOffByOneWordBoundary() {
            // Arrange: 1000 bits occupies 16 ulongs (1024 capacity), so index 1000 is inside the 16th ulong but > Length!
            using PooledBitArray bitArray = new(1000);

            // Act & Assert
            Assert.ThrowsAny<IndexOutOfRangeException>(() => bitArray.Get(1000));
            Assert.ThrowsAny<IndexOutOfRangeException>(() => bitArray.Set(1000));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-50)]
        public void Should_ThrowArgumentOutOfRangeException_When_LengthIsZeroOrNegative(long invalidLength) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new PooledBitArray(invalidLength));
        }
    }

    public sealed class DisposalLifecycleMethod {
        [Fact]
        public void Should_ThrowObjectDisposedException_When_OperationsCalledAfterDisposal() {
            // Arrange
            PooledBitArray bitArray = new(1024);
            bitArray.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => bitArray.Set(42));
            Assert.Throws<ObjectDisposedException>(() => bitArray.Get(42));
            Assert.Throws<ObjectDisposedException>(() => bitArray.GetPopCount());
            Assert.Throws<ObjectDisposedException>(() => bitArray.CalculateChecksum());
        }
    }
}