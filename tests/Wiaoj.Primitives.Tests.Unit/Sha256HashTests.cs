using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class Sha256HashTests {
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
        Sha256Hash hash = new(expectedBytes);

        // Assert
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
        Assert.Throws<ArgumentException>(() => new Sha256Hash(invalidBytes));
    }

    [Fact]
    public void Empty_ShouldReturnZeroFilledHash() {
        // Arrange
        var empty = Sha256Hash.Empty;

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
        Sha256Hash hash1 = new(bytes);
        Sha256Hash hash2 = new(bytes);

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

        if(bytes1[0] == bytes2[0]) bytes2[0] = (byte)(bytes1[0] + 1);

        Sha256Hash hash1 = new(bytes1);
        Sha256Hash hash2 = new(bytes2);

        // Assert
        Assert.False(hash1.Equals(hash2));
        Assert.False(hash1 == hash2);
        Assert.True(hash1 != hash2);
        Assert.NotEqual(hash1.GetHashCode(), hash2.GetHashCode());
    }

    #endregion

    #region Hesaplama (Computation) Testleri

    [Fact]
    public void Compute_ShouldMatchStandardDotNetSHA256() {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test-data-abc");

        // Act - Sizin struct'ınızın hesaplaması
        Sha256Hash resultStruct = Sha256Hash.Compute(data);

        // Act - Standart .NET hesaplaması (Referans)
        byte[] expectedBytes = SHA256.HashData(data);

        // Assert
        Assert.Equal(expectedBytes, resultStruct.AsSpan().ToArray());
        Assert.Equal(Convert.ToHexString(expectedBytes), resultStruct.ToString());
    }

    #endregion

    #region String Dönüşüm Testleri

    [Fact]
    public void ToString_ShouldReturnUppercaseHex() {
        // Arrange
        byte[] bytes = new byte[32];
        for(int i = 0; i < 32; i++) bytes[i] = 0xAB;
        Sha256Hash hash = new(bytes);

        // Act
        string result = hash.ToString();

        // Assert
        Assert.Equal(64, result.Length);
        Assert.Equal("ABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABAB", result);
    }

    #endregion 
}