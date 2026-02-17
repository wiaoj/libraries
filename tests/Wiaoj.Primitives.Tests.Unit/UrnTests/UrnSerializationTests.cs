using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit.UrnTests; 
public sealed class UrnSerializationTests {
    private readonly JsonSerializerOptions _options = new() { WriteIndented = false };

    [Fact]
    public void Serialize_ObjectWithUrn_ShouldWriteCorrectJson() {
        var dto = new { Id = Urn.Create("user", "1"), Name = "Test" };
        string json = JsonSerializer.Serialize(dto, _options);

        Assert.Equal("{\"Id\":\"urn:user:1\",\"Name\":\"Test\"}", json);
    }

    [Fact]
    public void Deserialize_ValidJson_ShouldCreateUrn() {
        string json = "\"urn:order:999\"";
        var urn = JsonSerializer.Deserialize<Urn>(json, _options);

        Assert.Equal("order", urn.Namespace.ToString());
        Assert.Equal("999", urn.Identity.ToString());
    }

    [Fact]
    public void Dictionary_UrnAsKey_ShouldWorkInJson() {
        var dict = new Dictionary<Urn, string>
        {
            { Urn.Create("k", "1"), "val1" },
            { Urn.Create("k", "2"), "val2" }
        };

        string json = JsonSerializer.Serialize(dict, _options);

        // Keyler JSON'da string olmalı
        Assert.Contains("\"urn:k:1\":\"val1\"", json);
        Assert.Contains("\"urn:k:2\":\"val2\"", json);

        var back = JsonSerializer.Deserialize<Dictionary<Urn, string>>(json, _options);
        Assert.NotNull(back);
        Assert.Equal(2, back.Count);
        Assert.Equal("val1", back[Urn.Parse("urn:k:1")]);
    }

    [Fact]
    public void Deserialize_InvalidFormat_ShouldThrowJsonException() {
        string json = "\"invalid-urn-format\"";
        // Bizim converter'ımız FormatException'ı JsonException'a çevirmeli
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Urn>(json));
    }

    [Fact]
    public void Deserialize_Null_ShouldReturnUrnEmpty() {
        string json = "null";
        var urn = JsonSerializer.Deserialize<Urn>(json);
        Assert.Equal(Urn.Empty, urn);
    }
}