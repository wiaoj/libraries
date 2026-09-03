using System.Text;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterHeaderTests {
    private readonly BloomFilterConfiguration _testConfig = new(
        FilterName.Parse("header-test"),
        expectedItems: 50_000,
        errorRate: 0.01,
        sizeInBits: 479_253,
        hashFunctionCount: 7,
        hashSeed: 12345
    );

    public sealed class SerializationMethod : BloomFilterHeaderTests {
        [Fact]
        public void Should_WriteAndReadHeader_Successfully() {
            // Arrange
            using MemoryStream stream = new();
            ulong expectedChecksum = 0xDEADBEEFCAFEBABE;

            // Act
            BloomFilterHeader.WriteHeader(stream, expectedChecksum, this._testConfig);
            stream.Position = 0;

            bool success = BloomFilterHeader.TryReadHeader(
                stream,
                out ulong readChecksum,
                out long readSize,
                out int readHashCount,
                out ulong readFingerprint);

            // Assert
            Assert.True(success);
            Assert.Equal(expectedChecksum, readChecksum);
            Assert.Equal(this._testConfig.SizeInBits, readSize);
            Assert.Equal(this._testConfig.HashFunctionCount, readHashCount);
            Assert.NotEqual(0UL, readFingerprint);
        }

        [Fact]
        public void Should_ReturnFalse_When_StreamIsTooShort() {
            // Arrange
            using MemoryStream stream = new([0x57, 0x42, 0x46]); // 3 bytes, less than HeaderSize (36 bytes)

            // Act
            bool success = BloomFilterHeader.TryReadHeader(
                stream,
                out _,
                out _,
                out _,
                out _);

            // Assert
            Assert.False(success);
        }

        [Fact]
        public void Should_ReturnFalse_When_MagicBytesAreInvalid() {
            // Arrange
            using MemoryStream stream = new();
            BloomFilterHeader.WriteHeader(stream, 123, this._testConfig);

            // Corrupt magic bytes
            byte[] data = stream.ToArray();
            data[0] = (byte)'X';
            using MemoryStream corruptedStream = new(data);

            // Act
            bool success = BloomFilterHeader.TryReadHeader(
                corruptedStream,
                out _,
                out _,
                out _,
                out _);

            // Assert
            Assert.False(success);
        }

        [Fact]
        public void Should_ReturnFalse_When_VersionIsNotSupported() {
            // Arrange: Header with unsupported Version 2
            using MemoryStream stream = new();
            using(BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true)) {
                writer.Write(BloomFilterHeader.Magic); // "WBF1"
                writer.Write(2);                       // Version 2 (Unsupported)
                writer.Write(0UL);                     // Checksum
                writer.Write(0L);                      // Size
                writer.Write(0);                       // HashCount
                writer.Write(0UL);                     // Fingerprint
            }
            stream.Position = 0;

            // Act
            bool success = BloomFilterHeader.TryReadHeader(
                stream,
                out _,
                out _,
                out _,
                out _);

            // Assert
            Assert.False(success);
        }
    }
}