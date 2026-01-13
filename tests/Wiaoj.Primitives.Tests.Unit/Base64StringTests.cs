using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class Base64StringTests {

    #region 1. Constants & Basic State

    [Fact]
    public void Empty_ShouldBeEmptyString() {
        Assert.Equal(string.Empty, Base64String.Empty.Value);
        Assert.Equal(0, Base64String.Empty.GetDecodedLength());
    }

    [Fact]
    public void Default_ShouldBeEmptyString() {
        Base64String def = default;
        Assert.Equal(string.Empty, def.Value);
        Assert.Equal(Base64String.Empty, def);
    }

    #endregion

    #region 2. Creation (Encoding)

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "Zg==")]
    [InlineData("fo", "Zm8=")]
    [InlineData("foo", "Zm9v")]
    [InlineData("foob", "Zm9vYg==")]
    [InlineData("fooba", "Zm9vYmE=")]
    [InlineData("foobar", "Zm9vYmFy")]
    public void FromUtf8_ShouldEncodeCorrectly_RFC4648(string input, string expected) {
        Base64String result = Base64String.FromUtf8(input);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void FromBytes_ShouldEncodeBinaryData() {
        byte[] data = { 0xFF, 0x00, 0xAA };
        // 11111111 00000000 10101010
        // FF -> /w== (partial) but combined logic:
        // Base64 of FF 00 AA -> /wCq
        string expected = Convert.ToBase64String(data);

        Base64String result = Base64String.FromBytes(data);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void FromBytes_Empty_ShouldReturnEmpty() {
        Base64String result = Base64String.FromBytes(ReadOnlySpan<byte>.Empty);
        Assert.Equal(Base64String.Empty, result);
    }

    [Fact]
    public void From_WithEncoding_ShouldWork() {
        // UTF-16 "A" -> 00 41 (LE) -> QQA= (Base64)
        Base64String result = Base64String.From("A", Encoding.Unicode);
        Assert.Equal("QQA=", result.Value);
    }

    #endregion

    #region 3. Parsing (String & Char Span)

    [Theory]
    [InlineData("Zm9v")]
    [InlineData("Zm9vYg==")]
    public void Parse_String_Valid_ShouldWork(string input) {
        Base64String b64 = Base64String.Parse(input);
        Assert.Equal(input, b64.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Zm9v$")] // Invalid char
    [InlineData("Z")]      // Invalid length (4n+1)
    [InlineData("Zm9v=")] // Invalid padding length
    public void Parse_String_Invalid_ShouldThrow(string? input) {
        if(input == null)
            Assert.ThrowsAny<ArgumentException>(() => Base64String.Parse(input!));
        else
            Assert.Throws<FormatException>(() => Base64String.Parse(input));
    }

    [Fact]
    public void TryParse_String_ShouldWork() {
        Assert.True(Base64String.TryParse("Zm9v", out Base64String result));
        Assert.Equal("Zm9v", result.Value);

        Assert.False(Base64String.TryParse("Zm9v#", out _));
        Assert.False(Base64String.TryParse((string?)null, out _));
    }

    [Fact]
    public void TryParse_Span_ShouldWork() {
        ReadOnlySpan<char> span = "Zm9v".AsSpan();
        Assert.True(Base64String.TryParse(span, out Base64String result));
        Assert.Equal("Zm9v", result.Value);
    }

    #endregion

    #region 4. Parsing (UTF-8 Bytes)

    [Fact]
    public void Parse_Utf8_Valid_ShouldWork() {
        byte[] bytes = Encoding.UTF8.GetBytes("Zm9v");
        Base64String b64 = Base64String.Parse(bytes);
        Assert.Equal("Zm9v", b64.Value);
    }

    [Fact]
    public void Parse_Utf8_Invalid_ShouldThrow() {
        byte[] bytes = Encoding.UTF8.GetBytes("Invalid$");
        Assert.Throws<FormatException>(() => Base64String.Parse(bytes));
    }

    [Fact]
    public void TryParse_Utf8_ShouldWork() {
        byte[] valid = Encoding.UTF8.GetBytes("Zm9v");
        byte[] invalid = Encoding.UTF8.GetBytes("Zm9v#");

        Assert.True(Base64String.TryParse(valid, out Base64String res));
        Assert.Equal("Zm9v", res.Value);

        Assert.False(Base64String.TryParse(invalid, out _));
    }

    #endregion

    #region 5. Decoding (To Bytes)

    [Theory]
    [InlineData("Zg==", "f")]
    [InlineData("Zm9v", "foo")]
    [InlineData("", "")]
    public void ToBytes_ShouldDecodeCorrectly(string base64, string expectedOriginal) {
        Base64String b64 = Base64String.Parse(base64);
        byte[] decoded = b64.ToBytes();

        string result = Encoding.UTF8.GetString(decoded);
        Assert.Equal(expectedOriginal, result);
    }

    [Fact]
    public void TryDecode_DestinationTooSmall_ShouldReturnFalse() {
        Base64String b64 = Base64String.FromUtf8("foo"); // "Zm9v" (4 chars -> 3 bytes needed)
        Span<byte> buffer = stackalloc byte[2]; // Too small

        // TryFromBase64String implementation in .NET usually returns false if buffer is too small
        bool success = b64.TryDecode(buffer, out int written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    [Fact]
    public void GetDecodedLength_ShouldBeAccurate() {
        // "Zm9v" -> 3 bytes
        Base64String b1 = Base64String.Parse("Zm9v");
        Assert.Equal(3, b1.GetDecodedLength());

        // "Zg==" -> 1 byte
        Base64String b2 = Base64String.Parse("Zg==");
        Assert.Equal(1, b2.GetDecodedLength());
    }

    #endregion

    #region 6. Formatting & IBufferWriter

    [Fact]
    public void WriteTo_BufferWriter_ShouldWriteUtf8Bytes() {
        Base64String b64 = Base64String.Parse("Zm9v"); // "foo"
        ArrayBufferWriter<byte> writer = new();

        b64.WriteTo(writer);

        string result = Encoding.UTF8.GetString(writer.WrittenSpan);
        Assert.Equal("Zm9v", result);
    }

    [Fact]
    public void ToString_ShouldReturnValue() {
        Base64String b64 = Base64String.Parse("ABC=");
        Assert.Equal("ABC=", b64.ToString());
    }

    #endregion

    #region 7. Equality & Operators

    [Fact]
    public void Equality_Logic() {
        Base64String b1 = Base64String.Parse("Zm9v");
        Base64String b2 = Base64String.Parse("Zm9v");
        Base64String b3 = Base64String.Parse("Zg==");

        Assert.True(b1.Equals(b2));
        Assert.False(b1.Equals(b3));
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
    }

    [Fact]
    public void ImplicitOperator_ToString_ShouldWork() {
        Base64String b64 = Base64String.Parse("ABC=");
        string s = b64;
        Assert.Equal("ABC=", s);
    }

    [Fact]
    public void ExplicitOperator_FromString_ShouldParse() {
        string input = "ABC=";
        Base64String b64 = (Base64String)input;
        Assert.Equal("ABC=", b64.Value);
    }

    [Fact]
    public void ExplicitOperator_InvalidString_ShouldThrow() {
        string input = "Invalid!";
        Assert.Throws<FormatException>(() => (Base64String)input);
    }

    #endregion

    #region 8. JSON Serialization

    private class TestModel {
        public Base64String Data { get; set; }
    }

    [Fact]
    public void Json_Serialize_ShouldWriteString() {
        TestModel model = new() { Data = Base64String.Parse("Zm9v") };
        string json = JsonSerializer.Serialize(model);

        Assert.Contains("\"Data\":\"Zm9v\"", json);
    }

    [Fact]
    public void Json_Deserialize_Valid_ShouldWork() {
        string json = "{\"Data\": \"Zm9v\"}";
        TestModel? model = JsonSerializer.Deserialize<TestModel>(json);

        Assert.Equal("Zm9v", model!.Data.Value);
    }

    [Fact]
    public void Json_Deserialize_Invalid_ShouldThrow() {
        string json = "{\"Data\": \"Inva!lid\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestModel>(json));
    }

    #endregion

    #region 9. Large Data & Memory Pooling (Stress Test)

    [Fact]
    public void LargeData_ShouldUseArrayPoolAndSucceed() {
        // Create > 1KB data to trigger ArrayPool usage in TryParseInternal logic
        byte[] largeData = new byte[2048];
        new Random(42).NextBytes(largeData); // Fill with random data

        string base64 = Convert.ToBase64String(largeData);

        // This will internally use the rented buffer path
        Base64String b64String = Base64String.Parse(base64);

        Assert.Equal(base64, b64String.Value);
        Assert.Equal(largeData, b64String.ToBytes());
    }

    #endregion
}