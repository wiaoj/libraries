using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit;

public sealed class HmacSha512HashTests {
    private static byte[] GetRandomBytes(int length = 32) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

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