using System.Security.Cryptography;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class HmacSha256HashJsonTests {
    private static byte[] GetRandomBytes(int length = 32) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    [Fact]
    public void Serialize_ShouldProduceHexString() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        var hash = new HmacSha256Hash(bytes);
        string expectedHex = Convert.ToHexString(bytes);

        // Act
        string json = JsonSerializer.Serialize(hash);

        // Assert
        // JSON string içinde tırnaklarla gelir: "DEADBEEF..."
        Assert.Equal($"\"{expectedHex}\"", json);
    }

    [Fact]
    public void Deserialize_ValidHex_ShouldWork() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        string hex = Convert.ToHexString(bytes);
        string json = $"\"{hex}\"";

        // Act
        var result = JsonSerializer.Deserialize<HmacSha256Hash>(json);

        // Assert
        Assert.Equal(bytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Deserialize_Null_ShouldReturnEmpty() {
        // Arrange
        string json = "null";

        // Act
        var result = JsonSerializer.Deserialize<HmacSha256Hash>(json);

        // Assert
        Assert.Equal(HmacSha256Hash.Empty, result);
    }

    [Theory]
    [InlineData("\"ZZZZ\"")] // Geçersiz hex karakter
    [InlineData("\"ABCD\"")] // Eksik uzunluk (64 char olmalı)
    [InlineData("12345")]   // String değil
    public void Deserialize_InvalidInput_ShouldThrowJsonException(string invalidJson) {
        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<HmacSha256Hash>(invalidJson));
    }

    [Fact]
    public void DictionaryKey_ShouldSerializeAsHexKey() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        var hash = new HmacSha256Hash(bytes);
        var dict = new Dictionary<HmacSha256Hash, string>
        {
            { hash, "my-value" }
        };

        // Act
        string json = JsonSerializer.Serialize(dict);

        // Assert
        // Beklenen format: {"HEX_KEY":"my-value"}
        string expectedKey = Convert.ToHexString(bytes);
        Assert.Contains(expectedKey, json);
        Assert.Contains("my-value", json);
    }

    [Fact]
    public void DictionaryKey_ShouldDeserializeFromHexKey() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        string hex = Convert.ToHexString(bytes);
        string json = $"{{\"{hex}\": \"success\"}}";

        // Act
        var dict = JsonSerializer.Deserialize<Dictionary<HmacSha256Hash, string>>(json);

        // Assert
        Assert.NotNull(dict);
        Assert.Single(dict);

        var key = dict.Keys.First();
        Assert.Equal(bytes, key.AsSpan().ToArray());
        Assert.Equal("success", dict[key]);
    }
}