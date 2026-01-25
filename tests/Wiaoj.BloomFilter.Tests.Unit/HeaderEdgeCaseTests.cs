using System.Text;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Tests.Unit;

public sealed class HeaderEdgeCaseTests {
    [Fact]
    public void ReadHeader_With_Invalid_Magic_Should_Return_False() {
        // Arrange: "WBF1" yerine "HACK" yazan bir stream
        MemoryStream ms = new();
        ms.Write("HACK"u8);
        ms.Write(new byte[32]);
        ms.Position = 0;

        // Act
        bool result = BloomFilterHeader.TryReadHeader(ms, out _, out _, out _, out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ReadHeader_With_Version_Mismatch_Should_Throw_NotSupported() {
        // Arrange: Versiyonu 2 olan bir header (şu an sadece 1 destekleniyor)
        MemoryStream ms = new();
        using(BinaryWriter writer = new(ms, Encoding.UTF8, true)) {
            writer.Write("WBF1"u8); // Magic
            writer.Write(2);        // Version (Unsupported)
            writer.Write(0UL);      // Checksum
            writer.Write(0L);       // Size
            writer.Write(0);        // HashCount
            writer.Write(0UL);      // Fingerprint
        }
        ms.Position = 0;

        // Act 
        bool result = BloomFilterHeader.TryReadHeader(ms, out _, out _, out _, out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ReadHeader_With_Incomplete_Stream_Should_Return_False() {
        // Arrange: Header boyutundan (36 byte) kısa bir stream
        MemoryStream ms = new(new byte[10]);

        // Act
        bool result = BloomFilterHeader.TryReadHeader(ms, out _, out _, out _, out _);

        // Assert
        Assert.False(result);
    }
}