using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class Base32StringTests {

    #region 1. Constants & Basic State

    [Fact]
    public void Empty_ShouldBeEmptyString() {
        Assert.Equal(string.Empty, Base32String.Empty.Value);
        Assert.Equal(0, Base32String.Empty.GetDecodedLength());
    }

    [Fact]
    public void Default_ShouldBeEmptyString() {
        Base32String def = default;
        Assert.Equal(string.Empty, def.Value);
        Assert.Equal(Base32String.Empty, def);
    }

    #endregion

    #region 2. Creation (Encoding)

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY======")]
    [InlineData("fo", "MZXQ====")]
    [InlineData("foo", "MZXW6===")]
    [InlineData("foob", "MZXW6YQ=")]
    [InlineData("foobar", "MZXW6YTBOI======")]
    public void FromUtf8_ShouldEncodeCorrectly_RFC4648(string input, string expected) {
        // Act
        Base32String result = Base32String.FromUtf8(input);

        // Assert
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void FromBytes_ShouldEncodeBinaryData() {
        byte[] data = { 0xFF, 0x00, 0xAA };
        // 11111111 00000000 10101010
        // 11111 11100 00000 01010 10100 (Bits separated by 5)
        // 31    28    0     10    20
        // 7     4     A     K     U
        // + Padding to 8 chars
        string expected = "74AKU===";

        Base32String result = Base32String.FromBytes(data);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void FromBytes_Empty_ShouldReturnEmpty() {
        Base32String result = Base32String.FromBytes(ReadOnlySpan<byte>.Empty);
        Assert.Equal(Base32String.Empty, result);
    }

    #endregion

    #region 3. Parsing (String & Char Span)

    [Theory]
    [InlineData("MZXW6YQ=")]
    [InlineData("mzxw6yq=")] // Lowercase handling
    [InlineData("MZXW6YQ")]  // Missing padding (handled gracefully if valid chars, though RFC prefers padding)
    public void Parse_String_Valid_ShouldWork(string input) {
        Base32String b32 = Base32String.Parse(input);
        Assert.Equal(input.ToUpperInvariant(), b32.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MZXW6YQ$")] // Invalid char
    [InlineData("12345678")] // '1' and '8' are not in Base32 alphabet
    [InlineData("ABC=DEF")]  // Padding in middle
    public void Parse_String_Invalid_ShouldThrow(string? input) {
        if(input == null)
            Assert.ThrowsAny<ArgumentNullException>(() => Base32String.Parse(input!));
        else
            Assert.Throws<FormatException>(() => Base32String.Parse(input));
    }

    [Fact]
    public void TryParse_String_ShouldWork() {
        Assert.True(Base32String.TryParse("ABC", out Base32String result));
        Assert.Equal("ABC", result.Value);

        Assert.False(Base32String.TryParse("AB#", out _));
        Assert.False(Base32String.TryParse((string?)null, out _));
    }

    [Fact]
    public void TryParse_Span_ShouldWork() {
        ReadOnlySpan<char> span = "abc".AsSpan();
        Assert.True(Base32String.TryParse(span, out Base32String result));
        Assert.Equal("ABC", result.Value); // Should normalize to upper
    }

    [Fact]
    public void TryParse_PaddingValidation() {
        // Valid padding
        Assert.True(Base32String.TryParse("AA======".AsSpan(), out _));

        // Invalid padding (padding followed by non-padding)
        Assert.False(Base32String.TryParse("AA=A".AsSpan(), out _));
    }

    #endregion

    #region 4. Parsing (UTF-8 Bytes)

    [Fact]
    public void Parse_Utf8_Valid_ShouldWork() {
        // "MZXW6YQ=" in bytes
        byte[] bytes = Encoding.UTF8.GetBytes("MZXW6YQ=");
        Base32String b32 = Base32String.Parse(bytes);
        Assert.Equal("MZXW6YQ=", b32.Value);
    }

    [Fact]
    public void Parse_Utf8_Invalid_ShouldThrow() {
        byte[] bytes = Encoding.UTF8.GetBytes("MZXW6YQ$");
        Assert.Throws<FormatException>(() => Base32String.Parse(bytes));
    }

    [Fact]
    public void TryParse_Utf8_ShouldWork() {
        byte[] valid = Encoding.UTF8.GetBytes("abc"); // Lowercase bytes
        byte[] invalid = Encoding.UTF8.GetBytes("123"); // '1' is invalid

        Assert.True(Base32String.TryParse(valid, out Base32String res));
        Assert.Equal("ABC", res.Value); // Normalized

        Assert.False(Base32String.TryParse(invalid, out _));
    }

    #endregion

    #region 5. Decoding (To Bytes)

    [Theory]
    [InlineData("MY======", "f")]
    [InlineData("MZXW6YQ=", "foob")]
    [InlineData("", "")]
    public void ToBytes_ShouldDecodeCorrectly(string base32, string expectedOriginal) {
        Base32String b32 = Base32String.Parse(base32);
        byte[] decoded = b32.ToBytes();

        string result = Encoding.UTF8.GetString(decoded);
        Assert.Equal(expectedOriginal, result);
    }

    [Fact]
    public void ToBytes_InvalidStructure_ShouldReturnEmptyOrPartial() {
        // Base32String ensures structure is valid at creation, so ToBytes operates on valid string.
        // However, TryDecode logic handles length checks.

        Base32String b32 = Base32String.Empty;
        Assert.Empty(b32.ToBytes());
    }

    [Fact]
    public void TryDecode_DestinationTooSmall_ShouldReturnFalse() {
        Base32String b32 = Base32String.FromUtf8("foo"); // "MZXW6===" (8 chars -> 5 bytes needed)
        Span<byte> buffer = stackalloc byte[2]; // Too small

        bool success = b32.TryDecode(buffer, out int written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    #endregion

    #region 6. Formatting & IBufferWriter

    [Fact]
    public void WriteTo_BufferWriter_ShouldWriteUtf8Bytes() {
        Base32String b32 = Base32String.Parse("ABC");
        ArrayBufferWriter<byte> writer = new();

        b32.WriteTo(writer);

        string result = Encoding.UTF8.GetString(writer.WrittenSpan);
        Assert.Equal("ABC", result);
    }

    [Fact]
    public void ToString_ShouldReturnValue() {
        Base32String b32 = Base32String.Parse("ABC");
        Assert.Equal("ABC", b32.ToString());
    }

    #endregion

    #region 7. Equality & Operators

    [Fact]
    public void Equality_ShouldBeCaseInsensitive_Logic() {
        // Since Parse normalizes to UpperCase immediately, strict equality works.
        Base32String b1 = Base32String.Parse("abc");
        Base32String b2 = Base32String.Parse("ABC");

        Assert.True(b1.Equals(b2));
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
    }

    [Fact]
    public void ImplicitOperator_ToString_ShouldWork() {
        Base32String b32 = Base32String.Parse("ABC");
        string s = b32;
        Assert.Equal("ABC", s);
    }

    [Fact]
    public void ImplicitOperator_ToSpan_ShouldWork() {
        Base32String b32 = Base32String.Parse("ABC");
        ReadOnlySpan<char> span = b32;
        Assert.Equal("ABC", span.ToString());
    }

    [Fact]
    public void ExplicitOperator_FromString_ShouldParse() {
        string input = "ABC";
        Base32String b32 = (Base32String)input;
        Assert.Equal("ABC", b32.Value);
    }

    [Fact]
    public void ExplicitOperator_InvalidString_ShouldThrow() {
        string input = "123"; // Invalid
        Assert.Throws<FormatException>(() => (Base32String)input);
    }

    #endregion

    #region 8. JSON Serialization

    private class TestModel {
        public Base32String Secret { get; set; }
    }

    [Fact]
    public void Json_Serialize_ShouldWriteString() {
        TestModel model = new() { Secret = Base32String.Parse("MZXW6===") };
        string json = JsonSerializer.Serialize(model);

        Assert.Contains("\"Secret\":\"MZXW6===\"", json);
    }

    [Fact]
    public void Json_Deserialize_Valid_ShouldWork() {
        string json = "{\"Secret\": \"MZXW6===\"}";
        TestModel? model = JsonSerializer.Deserialize<TestModel>(json);

        Assert.Equal("MZXW6===", model!.Secret.Value);
    }

    [Fact]
    public void Json_Deserialize_Invalid_ShouldThrow() {
        string json = "{\"Secret\": \"INVALID$\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestModel>(json));
    }

    #endregion
}