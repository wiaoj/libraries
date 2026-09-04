using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit.Hashing;

public sealed class Md5HashTests {
    private static byte[] GetRandomBytes(int length = 16) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    #region Temel Yapı ve Constructor Testleri

    [Fact]
    public void Constructor_Valid16Bytes_ShouldSucceed() {
        // Arrange
        byte[] expectedBytes = GetRandomBytes(16);

        // Act
        Md5Hash hash = new(expectedBytes);

        // Assert
        Assert.Equal(expectedBytes, hash.AsSpan().ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void Constructor_InvalidLength_ShouldThrowArgumentException(int length) {
        // Arrange
        byte[] invalidBytes = GetRandomBytes(length);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Md5Hash(invalidBytes));
    }

    [Fact]
    public void Empty_ShouldReturnZeroFilledHash() {
        // Arrange
        Md5Hash empty = Md5Hash.Empty;

        // Act
        byte[] bytes = empty.AsSpan().ToArray();

        // Assert
        Assert.Equal(16, bytes.Length);
        foreach (var b in bytes) {
            Assert.Equal(0, b);
        }
    }

    #endregion

    #region Hesaplama (Computation) Testleri

    [Fact]
    public void Compute_Bytes_ShouldMatchStandardDotNetMD5() {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test-data-md5");

        // Act
        Md5Hash result = Md5Hash.Compute(data);

        // Assert
        byte[] expectedBytes = MD5.HashData(data);
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
        Assert.Equal(Convert.ToHexString(expectedBytes), result.ToString());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_ShouldMatchBytesCompute() {
        // Arrange
        string text = "test-span-char-md5";
        byte[] expectedBytes = MD5.HashData(Encoding.UTF8.GetBytes(text));

        // Act
        Md5Hash result = Md5Hash.Compute(text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_WithEncoding_ShouldMatchBytesCompute() {
        // Arrange
        string text = "encoding-test-md5-şüöğçı";

        // Act & Assert - UTF-8
        byte[] utf8Expected = MD5.HashData(Encoding.UTF8.GetBytes(text));
        Assert.Equal(utf8Expected, Md5Hash.Compute(text.AsSpan(), Encoding.UTF8).AsSpan().ToArray());

        // Act & Assert - Unicode
        byte[] unicodeExpected = MD5.HashData(Encoding.Unicode.GetBytes(text));
        Assert.Equal(unicodeExpected, Md5Hash.Compute(text.AsSpan(), Encoding.Unicode).AsSpan().ToArray());

        // Act & Assert - ASCII
        byte[] asciiExpected = MD5.HashData(Encoding.ASCII.GetBytes(text));
        Assert.Equal(asciiExpected, Md5Hash.Compute(text.AsSpan(), Encoding.ASCII).AsSpan().ToArray());

        // Act & Assert - Latin1
        byte[] latin1Expected = MD5.HashData(Encoding.Latin1.GetBytes(text));
        Assert.Equal(latin1Expected, Md5Hash.Compute(text.AsSpan(), Encoding.Latin1).AsSpan().ToArray());
    }

    [Fact]
    public void Compute_String_ShouldMatchSpanCompute() {
        // Arrange
        string text = "string-vs-span-consistency-md5";

        // Act
        Md5Hash fromString = Md5Hash.Compute(text);
        Md5Hash fromSpan = Md5Hash.Compute(text.AsSpan());

        // Assert
        Assert.Equal(fromString, fromSpan);
    }

    [Fact]
    public void Compute_LargeString_ShouldMatchBytesCompute() {
        // Arrange - String larger than 1024 bytes to exercise ArrayPool fallback
        string text = new('D', 2048);
        byte[] expectedBytes = MD5.HashData(Encoding.UTF8.GetBytes(text));

        // Act
        Md5Hash result = Md5Hash.Compute(text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_UnderStackThreshold_ShouldProduceZeroAllocations() {
        // Arrange
        ReadOnlySpan<char> chars = "small-payload-for-zero-alloc-test".AsSpan();

        // Warmup (JIT)
        _ = Md5Hash.Compute(chars);

        // Act
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        _ = Md5Hash.Compute(chars);
        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        Assert.Equal(0, afterAllocated - beforeAllocated);
    }

    [Fact]
    public void Compute_String_UnderStackThreshold_ShouldProduceZeroAllocations() {
        // Arrange
        string text = "small-string-for-zero-alloc-test";

        // Warmup (JIT)
        _ = Md5Hash.Compute(text);

        // Act
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        _ = Md5Hash.Compute(text);
        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        Assert.Equal(0, afterAllocated - beforeAllocated);
    }

    [Fact]
    public void Compute_SecretChar_ShouldMatchBytesCompute() {
        // Arrange
        string text = "secret-char-md5-test";
        using Secret<char> secret = Secret<char>.From(text.AsSpan());
        byte[] expectedBytes = MD5.HashData(Encoding.UTF8.GetBytes(text));

        // Act
        Md5Hash result = Md5Hash.Compute(secret);

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_SecretChar_WithEncoding_ShouldMatchBytesCompute() {
        // Arrange
        string text = "secret-char-unicode-md5-test";
        using Secret<char> secret = Secret<char>.From(text.AsSpan());
        byte[] expectedBytes = MD5.HashData(Encoding.Unicode.GetBytes(text));

        // Act
        Md5Hash result = Md5Hash.Compute(secret, Encoding.Unicode);

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_SecretByte_ShouldMatchBytesCompute() {
        // Arrange
        byte[] data = GetRandomBytes(64);
        using Secret<byte> secret = Secret<byte>.From(data);
        byte[] expectedBytes = MD5.HashData(data);

        // Act
        Md5Hash result = Md5Hash.Compute(secret);

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    #endregion

    #region String Dönüşüm Testleri

    [Fact]
    public void ToString_ShouldReturnUppercaseHex() {
        // Arrange
        byte[] bytes = new byte[16];
        for (int i = 0; i < 16; i++) bytes[i] = 0xAB;
        Md5Hash hash = new(bytes);

        // Act
        string result = hash.ToString();

        // Assert
        Assert.Equal(32, result.Length);
        Assert.Equal("ABABABABABABABABABABABABABABABAB", result);
    }

    #endregion

    #region Asenkron Stream (ComputeAsync) Testleri

    [Fact]
    public async Task ComputeAsync_ShouldMatchStandardDotNetMD5_AndResetStreamPosition() {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("async-stream-test-data-for-md5");
        using MemoryStream ms = new(data);
        ms.Position = ms.Length;

        // Act
        Md5Hash resultStruct = await Md5Hash.ComputeAsync(ms);

        // Assert
        Assert.Equal(0, ms.Position);
        byte[] expectedBytes = MD5.HashData(data);
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
    }

    [Fact]
    public async Task ComputeAsync_NullStream_ShouldThrowArgumentNullException() {
        // Arrange
        Stream nullStream = null!;

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => Md5Hash.ComputeAsync(nullStream).AsTask());
    }

    #endregion
}
