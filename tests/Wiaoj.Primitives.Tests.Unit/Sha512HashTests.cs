using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class Sha512HashTests {
    private static byte[] GetRandomBytes(int length = 64) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    #region Temel Yapı ve Constructor Testleri

    [Fact]
    public void Constructor_Valid64Bytes_ShouldSucceed() {
        // Arrange
        byte[] expectedBytes = GetRandomBytes(64);

        // Act
        Sha512Hash hash = new(expectedBytes);

        // Assert
        Assert.Equal(expectedBytes, hash.AsSpan().ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(65)]
    public void Constructor_InvalidLength_ShouldThrowArgumentException(int length) {
        // Arrange
        byte[] invalidBytes = GetRandomBytes(length);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Sha512Hash(invalidBytes));
    }

    [Fact]
    public void Empty_ShouldReturnZeroFilledHash() {
        // Arrange
        Sha512Hash empty = Sha512Hash.Empty;

        // Act
        byte[] bytes = empty.AsSpan().ToArray();

        // Assert
        Assert.Equal(64, bytes.Length);
        foreach(var b in bytes) {
            Assert.Equal(0, b);
        }
    }

    #endregion

    #region Eşitlik (Equality) Testleri

    [Fact]
    public void Equality_SameContent_ShouldBeEqual() {
        // Arrange
        byte[] bytes = GetRandomBytes(64);
        Sha512Hash hash1 = new(bytes);
        Sha512Hash hash2 = new(bytes);

        // Assert
        Assert.True(hash1.Equals(hash2));
        Assert.True(hash1 == hash2);
        Assert.False(hash1 != hash2);
        Assert.Equal(hash1.GetHashCode(), hash2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentContent_ShouldNotBeEqual() {
        // Arrange
        byte[] bytes1 = GetRandomBytes(64);
        byte[] bytes2 = GetRandomBytes(64);

        if(bytes1[0] == bytes2[0]) bytes2[0] = (byte)(bytes1[0] + 1);

        Sha512Hash hash1 = new(bytes1);
        Sha512Hash hash2 = new(bytes2);

        // Assert
        Assert.False(hash1.Equals(hash2));
        Assert.False(hash1 == hash2);
        Assert.True(hash1 != hash2);
        Assert.NotEqual(hash1.GetHashCode(), hash2.GetHashCode());
    }

    #endregion

    #region Hesaplama (Computation) Testleri

    [Fact]
    public void Compute_ShouldMatchStandardDotNetSHA512() {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test-data-abc-1234567890");

        // Act - Sizin struct'ınızın hesaplaması
        Sha512Hash resultStruct = Sha512Hash.Compute(data);

        // Act - Standart .NET hesaplaması (Referans)
        byte[] expectedBytes = SHA512.HashData(data);

        // Assert
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
        Assert.Equal(Convert.ToHexString(expectedBytes), resultStruct.ToString());
    }

    #endregion

    #region String Dönüşüm Testleri

    [Fact]
    public void ToString_ShouldReturnUppercaseHex() {
        // Arrange
        byte[] bytes = new byte[64];
        for(int i = 0; i < 64; i++) bytes[i] = 0xAB;
        Sha512Hash hash = new(bytes);

        // Act
        string result = hash.ToString();

        // Assert
        Assert.Equal(128, result.Length); // 64 byte * 2 = 128 karakter
        string expectedHex = new string('A', 1).Replace("A", "AB");
        StringBuilder sb = new();
        for(int i = 0; i < 64; i++) sb.Append("AB");

        Assert.Equal(sb.ToString(), result);
    }

    #endregion 

    #region Asenkron Stream (ComputeAsync) Testleri

    [Fact]
    public async Task ComputeAsync_ShouldMatchStandardDotNetSHA512_AndResetStreamPosition() {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("async-stream-test-data-for-sha512");
        using MemoryStream ms = new(data);
        ms.Position = ms.Length;

        // Act
        Sha512Hash resultStruct = await Sha512Hash.ComputeAsync(ms);

        // Assert
        Assert.Equal(0, ms.Position);

        byte[] expectedBytes = SHA512.HashData(data);
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
    }

    [Fact]
    public async Task ComputeAsync_NullStream_ShouldThrowArgumentNullException() {
        // Arrange
        Stream nullStream = null!;

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => Sha512Hash.ComputeAsync(nullStream).AsTask());
    }

    #endregion
}