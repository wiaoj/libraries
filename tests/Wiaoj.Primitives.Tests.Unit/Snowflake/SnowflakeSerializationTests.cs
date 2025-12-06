using System.Text.Json;
using Wiaoj.Primitives.Snowflake;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;
public class SnowflakeSerializationTests {
    [Fact]
    public void Serialize_Writes_String() {
        var id = new SnowflakeId(1234567890123456789);
        string json = JsonSerializer.Serialize(id);

        // JS'de precision kaybı olmaması için string olmalı
        Assert.Equal($"\"{id.Value}\"", json);
    }

    [Fact]
    public void Deserialize_Reads_String() {
        string json = "\"1234567890123456789\"";
        var id = JsonSerializer.Deserialize<SnowflakeId>(json);

        Assert.Equal(1234567890123456789, id.Value);
    }

    [Fact]
    public void Deserialize_Reads_Number_Fallback() {
        // Eğer sayı olarak gelirse de (kayıp göze alınarak) okumalı
        string json = "100";
        var id = JsonSerializer.Deserialize<SnowflakeId>(json);

        Assert.Equal(100, id.Value);
    }

    [Fact]
    public void Dictionary_Key_Serialization_Works() {
        // Dictionary Key'i olarak kullanım
        var dict = new Dictionary<SnowflakeId, string> {
            { new SnowflakeId(1), "One" }
        };

        string json = JsonSerializer.Serialize(dict);
        // {"1":"One"}
        Assert.Contains("\"1\":", json);

        var back = JsonSerializer.Deserialize<Dictionary<SnowflakeId, string>>(json);
        Assert.Equal("One", back![new SnowflakeId(1)]);
    }
}