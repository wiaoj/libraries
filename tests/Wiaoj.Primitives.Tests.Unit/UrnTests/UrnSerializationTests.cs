using System.Text.Json;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.UrnTests;

public class UrnSerializationTests {
    [Fact]
    public void Serialize_Writes_String() {
        Urn urn = Urn.Create("test", "123");
        string json = JsonSerializer.Serialize(urn);

        Assert.Equal("\"urn:test:123\"", json);
    }

    [Fact]
    public void Deserialize_Reads_String() {
        string json = "\"urn:test:123\"";
        var urn = JsonSerializer.Deserialize<Urn>(json);

        Assert.Equal("test", urn.Namespace.ToString());
        Assert.Equal("123", urn.Identity.ToString());
    }

    [Fact]
    public void SnowflakeId_To_Urn_Integration() {
        // SnowflakeId'nin ToUrn metodu doğru çalışıyor mu?
        SnowflakeId id = new(12345);
        Urn urn = id.ToUrn("order");

        Assert.Equal("urn:order:12345", urn.ToString());
        Assert.Equal("order", urn.Namespace.ToString());
    }

    [Fact]
    public void Dictionary_Key_Serialization_Works() {
        // Urn bir Dictionary Key olabilir mi?
        Urn key = Urn.Create("k", "1");
        Dictionary<Urn, string> dict = new() {
            { key, "value" }
        };

        string json = JsonSerializer.Serialize(dict);
        // JSON'da key string olmalı: {"urn:k:1":"value"}
        Assert.Contains("\"urn:k:1\":", json);

        var back = JsonSerializer.Deserialize<Dictionary<Urn, string>>(json);
        Assert.Equal("value", back![key]);
    }
}