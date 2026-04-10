using System.Security.Cryptography;
using System.Text.Json;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Tests.Unit.Hashing;
public sealed class Sha256HashJsonTests {
    private static byte[] GetRandomBytes(int length = 32) {
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    [Fact]
    public void Serialize_ShouldProduceHexString() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        var hash = new Sha256Hash(bytes);
        string expectedHex = Convert.ToHexString(bytes);

        // Act
        string json = JsonSerializer.Serialize(hash);

        // Assert
        Assert.Equal($"\"{expectedHex}\"", json);
    }

    [Fact]
    public void Deserialize_ValidHex_ShouldWork() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        string hex = Convert.ToHexString(bytes);
        string json = $"\"{hex}\"";

        // Act
        var result = JsonSerializer.Deserialize<Sha256Hash>(json);

        // Assert
        Assert.Equal(bytes, result.AsSpan().ToArray());
    }

    [Fact]
    public void Deserialize_Null_ShouldReturnEmpty() {
        // Arrange
        string json = "null";

        // Act
        var result = JsonSerializer.Deserialize<Sha256Hash>(json);

        // Assert
        Assert.Equal(Sha256Hash.Empty, result);
    }

    [Theory]
    [InlineData("\"ZZZZ\"")]
    [InlineData("\"ABCD\"")]
    [InlineData("12345")]
    public void Deserialize_InvalidInput_ShouldThrowJsonException(string invalidJson) {
        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Sha256Hash>(invalidJson));
    }

    [Fact]
    public void DictionaryKey_ShouldSerializeAsHexKey() {
        // Arrange
        byte[] bytes = GetRandomBytes(32);
        var hash = new Sha256Hash(bytes);
        var dict = new Dictionary<Sha256Hash, string>
        {
            { hash, "my-value" }
        };

        // Act
        string json = JsonSerializer.Serialize(dict);

        // Assert
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
        var dict = JsonSerializer.Deserialize<Dictionary<Sha256Hash, string>>(json);

        // Assert
        Assert.NotNull(dict);
        Assert.Single(dict);

        var key = dict.Keys.First();
        Assert.Equal(bytes, key.AsSpan().ToArray());
        Assert.Equal("success", dict[key]);
    }
}