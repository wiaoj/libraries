using System.Text.Json;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit;   
public class EncodingSerializationTests {

    // --- HEX STRING ---
    [Fact]
    public void HexString_Json_RoundTrip() {
        HexString hex = HexString.FromUtf8("hello"); // "68656c6c6f"
        string json = JsonSerializer.Serialize(hex);

        Assert.Equal("\"68656c6c6f\"", json);

        var deserialized = JsonSerializer.Deserialize<HexString>(json);
        Assert.Equal(hex, deserialized);
    }

    // --- BASE64 STRING ---
    [Fact]
    public void Base64String_Json_RoundTrip() {
        Base64String b64 = Base64String.FromUtf8("hello"); // "SGVsbG8="
        string json = JsonSerializer.Serialize(b64);

        Assert.Equal("\"SGVsbG8=\"", json);

        var deserialized = JsonSerializer.Deserialize<Base64String>(json);
        Assert.Equal(b64, deserialized);
    }

    // --- BASE32 STRING ---
    [Fact]
    public void Base32String_Json_RoundTrip() {
        Base32String b32 = Base32String.FromUtf8("hello"); // "NBSWY3DP"
        string json = JsonSerializer.Serialize(b32);

        Assert.Equal("\"NBSWY3DP\"", json);

        var deserialized = JsonSerializer.Deserialize<Base32String>(json);
        Assert.Equal(b32, deserialized);
    }

    // --- OBJECT PROPERTY TEST ---
    private class TestDto {
        public SnowflakeId Id { get; set; }
        public Base64String Data { get; set; }
    }

    [Fact]
    public void ComplexObject_Serialization() {
        TestDto dto = new() {
            Id = new SnowflakeId(123),
            Data = Base64String.FromUtf8("test")
        };

        string json = JsonSerializer.Serialize(dto);
        Assert.Contains("\"Id\":\"123\"", json);
        Assert.Contains("\"Data\":\"dGVzdA==\"", json);

        var back = JsonSerializer.Deserialize<TestDto>(json);
        Assert.Equal(dto.Id, back!.Id);
        Assert.Equal(dto.Data, back.Data);
    }
}