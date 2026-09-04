using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit.Hashing;

public sealed class HmacSha256HashTests {
    // Testlerde kullanmak için rastgele byte dizisi üreten yardımcı metot
    private static byte[] GetRandomBytes(int length = 32) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    #region Temel Yapı ve Constructor Testleri

    [Fact]
    public void Constructor_Valid32Bytes_ShouldSucceed() {
        // Arrange
        byte[] expectedBytes = GetRandomBytes(32);

        // Act
        HmacSha256Hash hash = new(expectedBytes);

        // Assert
        // Span karşılaştırması için ToArray kullanıyoruz
        Assert.Equal(expectedBytes, hash.AsSpan().ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void Constructor_InvalidLength_ShouldThrowArgumentException(int length) {
        // Arrange
        byte[] invalidBytes = GetRandomBytes(length);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HmacSha256Hash(invalidBytes));
    }

    [Fact]
    public void Empty_ShouldReturnZeroFilledHash() {
        // Arrange
        HmacSha256Hash empty = HmacSha256Hash.Empty;

        // Act
        byte[] bytes = empty.AsSpan().ToArray();

        // Assert
        Assert.Equal(32, bytes.Length);
        foreach(var b in bytes) {
            Assert.Equal(0, b);
        }
    }

    #endregion

    #region Eşitlik (Equality) Testleri

    [Fact]
    public void Equality_SameContent_ShouldBeEqual() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        HmacSha256Hash hash1 = new(bytes);
        HmacSha256Hash hash2 = new(bytes);

        // Assert
        Assert.True(hash1.Equals(hash2));
        Assert.True(hash1 == hash2);
        Assert.False(hash1 != hash2);
        Assert.Equal(hash1.GetHashCode(), hash2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentContent_ShouldNotBeEqual() {
        // Arrange
        byte[] bytes1 = GetRandomBytes(32);
        byte[] bytes2 = GetRandomBytes(32);

        // Çok düşük ihtimalle aynı gelirse diye ilk byte'ı değiştiriyoruz
        if(bytes1[0] == bytes2[0]) bytes2[0] = (byte)(bytes1[0] + 1);

        HmacSha256Hash hash1 = new(bytes1);
        HmacSha256Hash hash2 = new(bytes2);

        // Assert
        Assert.False(hash1.Equals(hash2));
        Assert.False(hash1 == hash2);
        Assert.True(hash1 != hash2);
        Assert.NotEqual(hash1.GetHashCode(), hash2.GetHashCode());
    }

    #endregion

    #region Sıralama (Comparison) Testleri

    [Fact]
    public void CompareTo_SmallerFirstByteDiffers_ShouldReturnNegative() {
        // Arrange
        byte[] smaller = new byte[32]; smaller[0] = 0x01;
        byte[] larger = new byte[32]; larger[0] = 0x02;
        HmacSha256Hash a = new(smaller);
        HmacSha256Hash b = new(larger);

        // Assert
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
        Assert.Equal(0, a.CompareTo(a));
    }

    [Fact]
    public void ComparisonOperators_ShouldReflectByteOrdering() {
        // Arrange
        byte[] smaller = new byte[32]; smaller[0] = 0x01;
        byte[] larger = new byte[32]; larger[0] = 0x02;
        HmacSha256Hash a = new(smaller);
        HmacSha256Hash b = new(larger);

        // Assert
        Assert.True(a < b);
        Assert.True(b > a);
        Assert.True(a <= b);
        Assert.True(a <= a);
        Assert.True(b >= a);
        Assert.True(a >= a);
        Assert.False(a > b);
        Assert.False(b < a);
    }

    [Fact]
    public void CompareTo_Object_NullShouldReturnPositive() {
        // Arrange
        HmacSha256Hash a = HmacSha256Hash.Compute("key"u8.ToArray(), "data");

        // Act & Assert
        Assert.True(((IComparable)a).CompareTo(null) > 0);
    }

    [Fact]
    public void CompareTo_Object_WrongTypeShouldThrow() {
        // Arrange
        HmacSha256Hash a = HmacSha256Hash.Compute("key"u8.ToArray(), "data");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ((IComparable)a).CompareTo("not-a-hash"));
    }

    #endregion

    #region Hesaplama (Computation) Testleri

    [Fact]
    public void Compute_ShouldMatchStandardDotNetHMAC() {
        // Arrange
        byte[] key = Encoding.UTF8.GetBytes("test-key-123");
        byte[] data = Encoding.UTF8.GetBytes("test-data-abc");

        // Act - Sizin struct'ınızın hesaplaması
        HmacSha256Hash resultStruct = HmacSha256Hash.Compute(key, data);

        // Act - Standart .NET hesaplaması (Referans)
        byte[] expectedBytes = HMACSHA256.HashData(key, data);

        // Assert
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
        Assert.Equal(Convert.ToHexString(expectedBytes), resultStruct.ToString());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_WithByteKey_ShouldMatchBytesCompute() {
        // Arrange
        byte[] key = GetRandomBytes(32);
        string text = "test-span-char-hmac-data";
        byte[] expectedBytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));

        // Act
        HmacSha256Hash result = HmacSha256Hash.Compute(key.AsSpan(), text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_WithByteKey_AndEncoding_ShouldMatchBytesCompute() {
        // Arrange
        byte[] key = GetRandomBytes(32);
        string text = "encoding-test-şüöğçı";
        byte[] utf8Expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));
        byte[] unicodeExpected = HMACSHA256.HashData(key, Encoding.Unicode.GetBytes(text));

        // Act & Assert
        Assert.Equal(utf8Expected, HmacSha256Hash.Compute(key.AsSpan(), text.AsSpan(), Encoding.UTF8).AsSpan().ToArray());
        Assert.Equal(unicodeExpected, HmacSha256Hash.Compute(key.AsSpan(), text.AsSpan(), Encoding.Unicode).AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_WithSecretKey_ShouldMatchBytesCompute() {
        // Arrange
        byte[] keyBytes = GetRandomBytes(32);
        using Secret<byte> key = Secret<byte>.From(keyBytes);
        string text = "test-span-char-secret-key";
        byte[] expectedBytes = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(text));

        // Act
        HmacSha256Hash result = HmacSha256Hash.Compute(key, text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_String_ShouldMatchSpanCompute() {
        // Arrange
        byte[] key = GetRandomBytes(32);
        string text = "string-vs-span-consistency";

        // Act
        HmacSha256Hash fromString = HmacSha256Hash.Compute(key.AsSpan(), text);
        HmacSha256Hash fromSpan = HmacSha256Hash.Compute(key.AsSpan(), text.AsSpan());

        // Assert
        Assert.Equal(fromString, fromSpan);
    }

    [Fact]
    public void Compute_LargeString_ShouldMatchBytesCompute() {
        // Arrange - String larger than 1024 bytes to exercise ArrayPool fallback
        byte[] key = GetRandomBytes(32);
        string text = new('B', 2048);
        byte[] expectedBytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));

        // Act
        HmacSha256Hash result = HmacSha256Hash.Compute(key.AsSpan(), text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    #endregion

    #region String Dönüşüm Testleri

    [Fact]
    public void ToString_ShouldReturnUppercaseHex() {
        // Arrange
        byte[] bytes = new byte[32];
        for(int i = 0; i < 32; i++) bytes[i] = 0xAB; // Hepsi AB
        HmacSha256Hash hash = new(bytes);

        // Act
        string result = hash.ToString();

        // Assert
        Assert.Equal(64, result.Length);
        Assert.Equal("ABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABAB", result);
    }

    #endregion 

    #region Asenkron Stream (ComputeAsync) Testleri

    [Fact]
    public async Task ComputeAsync_ShouldMatchStandardDotNetHMAC_AndResetStreamPosition() {
        // Arrange
        byte[] keyBytes = GetRandomBytes(32);
        using var key = Secret<byte>.From(keyBytes);
        byte[] data = Encoding.UTF8.GetBytes("async-stream-test-data-for-hmac-256");

        using MemoryStream ms = new(data);
        // Stream'i bilerek sona alıyoruz ki metodun en başında Position = 0 yapıp yapmadığını görelim
        ms.Position = ms.Length;

        // Act
        HmacSha256Hash resultStruct = await HmacSha256Hash.ComputeAsync(ms, key);

        // Assert
        // 1. İşlem bittiğinde stream başa sarılmış olmalı! (En önemli acceptance criteria)
        Assert.Equal(0, ms.Position);

        // 2. Hash sonucu .NET'in standardı ile aynı olmalı
        byte[] expectedBytes = HMACSHA256.HashData(keyBytes, data);
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
    }

    [Fact]
    public async Task ComputeAsync_NullStream_ShouldThrowArgumentNullException() {
        // Arrange
        Stream nullStream = null!;
        using var key = Secret<byte>.From(GetRandomBytes(32));

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => HmacSha256Hash.ComputeAsync(nullStream, key).AsTask());
    }
    #endregion
}