using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit.Hashing;

public sealed class HmacSha512HashTests {
    private static byte[] GetRandomBytes(int length = 32) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    #region Hesaplama (Computation) Testleri

    [Fact]
    public void Compute_ReadOnlySpanChar_WithByteKey_ShouldMatchBytesCompute() {
        // Arrange
        byte[] key = GetRandomBytes(64);
        string text = "test-span-char-hmac512-data";
        byte[] expectedBytes = HMACSHA512.HashData(key, Encoding.UTF8.GetBytes(text));

        // Act
        HmacSha512Hash result = HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_WithByteKey_AndEncoding_ShouldMatchBytesCompute() {
        // Arrange
        byte[] key = GetRandomBytes(64);
        string text = "encoding-test-şüöğçı";
        byte[] utf8Expected = HMACSHA512.HashData(key, Encoding.UTF8.GetBytes(text));
        byte[] unicodeExpected = HMACSHA512.HashData(key, Encoding.Unicode.GetBytes(text));
        byte[] asciiExpected = HMACSHA512.HashData(key, Encoding.ASCII.GetBytes(text));
        byte[] latin1Expected = HMACSHA512.HashData(key, Encoding.Latin1.GetBytes(text));

        // Act & Assert
        Assert.Equal(utf8Expected, HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan(), Encoding.UTF8).AsSpan().ToArray());
        Assert.Equal(unicodeExpected, HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan(), Encoding.Unicode).AsSpan().ToArray());
        Assert.Equal(asciiExpected, HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan(), Encoding.ASCII).AsSpan().ToArray());
        Assert.Equal(latin1Expected, HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan(), Encoding.Latin1).AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_WithSecretKey_ShouldMatchBytesCompute() {
        // Arrange
        byte[] keyBytes = GetRandomBytes(64);
        using Secret<byte> key = Secret<byte>.From(keyBytes);
        string text = "test-span-char-secret-key-512";
        byte[] expectedBytes = HMACSHA512.HashData(keyBytes, Encoding.UTF8.GetBytes(text));

        // Act
        HmacSha512Hash result = HmacSha512Hash.Compute(key, text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_String_ShouldMatchSpanCompute() {
        // Arrange
        byte[] key = GetRandomBytes(64);
        string text = "string-vs-span-consistency";

        // Act
        HmacSha512Hash fromString = HmacSha512Hash.Compute(key.AsSpan(), text);
        HmacSha512Hash fromSpan = HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan());

        // Assert
        Assert.Equal(fromString, fromSpan);
    }

    [Fact]
    public void Compute_LargeString_ShouldMatchBytesCompute() {
        // Arrange - String larger than 1024 bytes to exercise ArrayPool fallback
        byte[] key = GetRandomBytes(64);
        string text = new('C', 2048);
        byte[] expectedBytes = HMACSHA512.HashData(key, Encoding.UTF8.GetBytes(text));

        // Act
        HmacSha512Hash result = HmacSha512Hash.Compute(key.AsSpan(), text.AsSpan());

        // Assert
        Assert.Equal(expectedBytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Compute_ReadOnlySpanChar_UnderStackThreshold_ShouldProduceZeroAllocations() {
        // Arrange
        byte[] key = GetRandomBytes(64);
        ReadOnlySpan<char> chars = "small-payload-for-zero-alloc-test".AsSpan();

        // Warmup (JIT)
        _ = HmacSha512Hash.Compute(key.AsSpan(), chars);

        // Act
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        _ = HmacSha512Hash.Compute(key.AsSpan(), chars);
        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        Assert.Equal(0, afterAllocated - beforeAllocated);
    }

    [Fact]
    public void Compute_String_UnderStackThreshold_ShouldProduceZeroAllocations() {
        // Arrange
        byte[] key = GetRandomBytes(64);
        string text = "small-string-for-zero-alloc-test";

        // Warmup (JIT)
        _ = HmacSha512Hash.Compute(key.AsSpan(), text);

        // Act
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        _ = HmacSha512Hash.Compute(key.AsSpan(), text);
        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        Assert.Equal(0, afterAllocated - beforeAllocated);
    }

    [Fact]
    public void Compute_SecretKey_UnderStackThreshold_ShouldProduceZeroAllocations() {
        // Arrange
        byte[] keyBytes = GetRandomBytes(64);
        using Secret<byte> key = Secret<byte>.From(keyBytes);
        ReadOnlySpan<char> chars = "small-payload-for-zero-alloc-test".AsSpan();

        // Warmup (JIT)
        _ = HmacSha512Hash.Compute(key, chars);

        // Act
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        _ = HmacSha512Hash.Compute(key, chars);
        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        Assert.Equal(0, afterAllocated - beforeAllocated);
    }

    #endregion

    #region Asenkron Stream (ComputeAsync) Testleri

    [Fact]
    public async Task ComputeAsync_ShouldMatchStandardDotNetHMAC_AndResetStreamPosition() {
        // Arrange
        byte[] keyBytes = GetRandomBytes(64);
        using Secret<byte> key = Secret<byte>.From(keyBytes);
        byte[] data = Encoding.UTF8.GetBytes("async-stream-test-data-for-hmac-512");

        using MemoryStream ms = new(data);
        ms.Position = ms.Length;

        // Act
        HmacSha512Hash resultStruct = await HmacSha512Hash.ComputeAsync(ms, key);

        // Assert
        Assert.Equal(0, ms.Position);

        byte[] expectedBytes = HMACSHA512.HashData(keyBytes, data);
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
    }

    #endregion
}