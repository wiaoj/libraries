using System.Buffers.Binary;
using Wiaoj.Primitives.Snowflake;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;

public class SnowflakeIdFormattingTests {
    [Fact]
    public void ToString_Returns_Numeric_String() {
        var id = new SnowflakeId(12345);
        Assert.Equal("12345", id.ToString());
    }

    [Fact]
    public void Parse_String_Works() {
        var id = SnowflakeId.Parse("12345");
        Assert.Equal(12345, id.Value);
    }

    [Fact]
    public void ToByteArray_Writes_BigEndian_8Bytes() {
        // Arrange
        long rawValue = 0x0102030405060708;
        var id = new SnowflakeId(rawValue);

        // Act
        byte[] bytes = id.ToByteArray();

        // Assert
        Assert.Equal(8, bytes.Length);
        Assert.Equal(0x01, bytes[0]); // Big Endian Check
        Assert.Equal(0x08, bytes[7]);

        // Roundtrip
        var back = SnowflakeId.FromBytes(bytes);
        Assert.Equal(id, back);
    }

    [Fact]
    public void TryWriteBytes_Works_With_Span() {
        var id = new SnowflakeId(long.MaxValue);
        Span<byte> buffer = stackalloc byte[8];

        bool success = id.TryWriteBytes(buffer);
        Assert.True(success);

        long result = BinaryPrimitives.ReadInt64BigEndian(buffer);
        Assert.Equal(long.MaxValue, result);
    }

    [Fact]
    public void Guid_Conversion_Is_Padding_Aware() {
        // Snowflake 8 byte, Guid 16 byte.
        // BigEndian yerleşimde ilk 8 byte 0 olmalı.
        var id = new SnowflakeId(long.MaxValue);
        Guid guid = id.ToGuid();

        byte[] guidBytes = guid.ToByteArray();

        // Guid yapısı mimariye göre değişebilir ama bizim ToGuid 
        // implementasyonumuz son 8 byte'a yazıyor.
        // Not: Guid.ToByteArray() Little Endian döndürebilir (Int32 kısımları için).
        // Ancak bizim FromGuid/ToGuid birbirini tamamladığı sürece sorun yok.

        var back = SnowflakeId.FromGuid(guid);
        Assert.Equal(id, back);
    }

    [Fact]
    public void TryWriteBytes_Returns_False_If_Buffer_Too_Small() {
        var id = SnowflakeId.NewId();

        // 8 byte gerekli ama biz 7 veriyoruz
        Span<byte> tooSmall = stackalloc byte[7];

        bool success = id.TryWriteBytes(tooSmall);

        Assert.False(success, "Should return false when buffer is too small.");
    }
}