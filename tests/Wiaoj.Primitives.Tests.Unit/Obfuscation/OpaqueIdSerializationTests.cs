using System.Text.Json;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation; 
public sealed class OpaqueIdSerializationTests {
    public OpaqueIdSerializationTests() {
        OpaqueId.Configure(new FeistelBase62Obfuscator(new() { Seed = "1234567890123456"u8.ToArray() }));
    }

    private class UserDto {
        public OpaqueId Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void Json_RoundTrip_Should_Maintain_Obfuscation() {
        OpaqueId originalId = new(1234567L);
        var dto = new UserDto { Id = originalId, Name = "wiaoj" };

        string json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("1234567", json);

        var deserialized = JsonSerializer.Deserialize<UserDto>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(originalId, deserialized.Id);
    }

    [Fact]
    public void Dictionary_Key_Serialization_Should_Work() {
        var dict = new Dictionary<OpaqueId, string> {
            { new OpaqueId(Guid.NewGuid()), "Value1" }
        };

        string json = JsonSerializer.Serialize(dict);
        Assert.Contains(":\"Value1\"", json);

        var back = JsonSerializer.Deserialize<Dictionary<OpaqueId, string>>(json);
        Assert.NotNull(back);
        Assert.Equal("Value1", back.Values.First());
    }

    [Fact]
    public void Null_Or_Empty_Json_String_Should_Deserialize_As_Empty() { 
        string json = "\"\"";
        var result = JsonSerializer.Deserialize<OpaqueId>(json);

        Assert.Equal(OpaqueId.Empty, result);
    }

    [Fact]
    public void Null_Json_Token_Should_Return_Empty() { 
        string json = "null";
        var result = JsonSerializer.Deserialize<OpaqueId>(json);

        Assert.Equal(OpaqueId.Empty, result);
    }
}