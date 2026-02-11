using System.Text.Json;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation; 
public sealed class PublicIdSerializationTests {
    private class UserDto {
        public PublicId Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void Json_RoundTrip_Should_Maintain_Obfuscation() {
        PublicId originalId = new(1234567L);
        var dto = new UserDto { Id = originalId, Name = "wiaoj" };

        string json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("1234567", json);

        var deserialized = JsonSerializer.Deserialize<UserDto>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(originalId, deserialized.Id);
    }

    [Fact]
    public void Dictionary_Key_Serialization_Should_Work() {
        var dict = new Dictionary<PublicId, string> {
            { new PublicId(Guid.NewGuid()), "Value1" }
        };

        string json = JsonSerializer.Serialize(dict);
        Assert.Contains(":\"Value1\"", json);

        var back = JsonSerializer.Deserialize<Dictionary<PublicId, string>>(json);
        Assert.NotNull(back);
        Assert.Equal("Value1", back.Values.First());
    }

    [Fact]
    public void Null_Or_Empty_Json_String_Should_Deserialize_As_Empty() { 
        string json = "\"\"";
        var result = JsonSerializer.Deserialize<PublicId>(json);

        Assert.Equal(PublicId.Empty, result);
    }

    [Fact]
    public void Null_Json_Token_Should_Return_Empty() { 
        string json = "null";
        var result = JsonSerializer.Deserialize<PublicId>(json);

        Assert.Equal(PublicId.Empty, result);
    }
}