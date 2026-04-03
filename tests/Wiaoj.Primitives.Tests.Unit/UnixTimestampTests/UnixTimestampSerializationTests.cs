using System.Text;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit.UnixTimestampTests;

public sealed class UnixTimestampSerializationTests {

    [Theory]
    [InlineData("0", 0)]
    [InlineData("123456", 123456)]
    [InlineData("-1", -1)]
    public void Parse_String_Valid_ShouldWork(string input, long expected) {
        UnixTimestamp result = UnixTimestamp.Parse(input);
        Assert.Equal(expected, result.TotalMilliseconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Parse_String_Invalid_ShouldThrow(string? input) {
        if(input == null) Assert.ThrowsAny<ArgumentException>(() => UnixTimestamp.Parse(input!));
        else Assert.Throws<FormatException>(() => UnixTimestamp.Parse(input));
    }

    [Fact]
    public void TryParse_String_ShouldWork() {
        Assert.True(UnixTimestamp.TryParse("100", out var ts) && ts == 100);
        Assert.False(UnixTimestamp.TryParse("invalid", out _));
        Assert.False(UnixTimestamp.TryParse((string?)null, out _));
    }

    [Fact]
    public void TryParse_Span_ShouldWork() {
        Assert.True(UnixTimestamp.TryParse("999".AsSpan(), out var ts) && ts == 999);
    }

    [Fact]
    public void TryFormat_Utf8_ShouldWriteCorrectIsoString() {
        UnixTimestamp ts = UnixTimestamp.Epoch;
        Span<byte> buffer = stackalloc byte[32];

        bool success = ((IUtf8SpanFormattable)ts).TryFormat(buffer, out int bytesWritten, default, null);
        string result = Encoding.UTF8.GetString(buffer[..bytesWritten]);

        Assert.True(success);
        Assert.Equal("1970-01-01T00:00:00.000Z", result);
    }

    [Fact]
    public void Explicit_IParsable_ShouldWork() {
        Assert.Equal(555, TestHelpers.ParseHelper<UnixTimestamp>("555").TotalMilliseconds);
    }

    [Fact]
    public void Explicit_ISpanParsable_ShouldWork() {
        Assert.Equal(777, TestHelpers.ParseSpanHelper<UnixTimestamp>("777").TotalMilliseconds);
    }

    [Fact]
    public void Explicit_IUtf8SpanParsable_Parse_ShouldWork() {
        Assert.Equal(12345, TestHelpers.ParseUtf8Helper<UnixTimestamp>("12345").TotalMilliseconds);
    }

    [Fact]
    public void Explicit_IUtf8SpanParsable_TryParse_ShouldWork() {
        byte[] valid = "500"u8.ToArray();
        Assert.True(TestHelpers.TryParseUtf8Helper<UnixTimestamp>(valid, out var ts) && ts == 500);
    }

    [Fact]
    public void ToString_ShouldReturnIso8601ByDefault() {
        var ts = UnixTimestamp.FromMilliseconds(0);
        Assert.Equal("1970-01-01T00:00:00.000Z", ts.ToString());
    }

    [Fact]
    public void ToString_FormatR_ShouldReturnRawNumber() {
        var ts = UnixTimestamp.FromMilliseconds(12345);
        Assert.Equal("12345", ts.ToString("R"));
    }

    [Fact]
    public void ToString_CustomFormat_ShouldWork() {
        var ts = UnixTimestamp.FromMilliseconds(1672531200000); // 2023-01-01
        Assert.Equal("2023", ts.ToString("yyyy"));
    }

    [Fact]
    public void ToString_WithCustomFormat_ShouldReturnFormattedDateTime() {
        // 2023-01-01 15:30:00
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1672587000000);

        string result = ts.ToString("yyyy-MM-dd HH:mm:ss");

        Assert.Equal("2023-01-01 15:30:00", result);
    }

    [Fact]
    public void ToString_OutOfRange_ShouldFallbackToRawWithoutCrashing() {
        UnixTimestamp extreme = (UnixTimestamp)long.MaxValue;
        Assert.Equal(long.MaxValue.ToString(), extreme.ToString());
    }

    [Fact]
    public void Explicit_ISpanFormattable_TryFormat_ShouldWork() {
        Span<char> buffer = stackalloc char[32];
        Assert.True(((ISpanFormattable)UnixTimestamp.Epoch).TryFormat(buffer, out int w, default, null));
        Assert.Contains("1970", buffer[..w].ToString());
    }

    [Fact]
    public void Explicit_IUtf8SpanFormattable_TryFormat_ShouldWork() {
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(((IUtf8SpanFormattable)UnixTimestamp.Epoch).TryFormat(buffer, out int w, default, null));
        Assert.Contains("1970", Encoding.UTF8.GetString(buffer[..w]));
    }

    [Fact]
    public void Json_Serialize_ShouldWriteNumber() {
        string json = JsonSerializer.Serialize((UnixTimestamp)123456);
        Assert.Equal("123456", json);
    }

    [Fact]
    public void Json_Deserialize_Number_ShouldWork() {
        Assert.Equal(999, JsonSerializer.Deserialize<UnixTimestamp>("999").TotalMilliseconds);
    }

    [Fact]
    public void Json_Deserialize_String_ShouldWork_Fallback() {
        Assert.Equal(888, JsonSerializer.Deserialize<UnixTimestamp>("\"888\"").TotalMilliseconds);
    }

    [Fact]
    public void Json_DictionaryKey_ShouldBeRawNumberString() {
        var dict = new Dictionary<UnixTimestamp, string> { { (UnixTimestamp)100, "V1" } };
        string json = JsonSerializer.Serialize(dict);
        Assert.Contains("\"100\":", json);
        Assert.DoesNotContain("1970-01-01", json);
    }
}