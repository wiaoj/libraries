using System.Text;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;

public sealed class UnixTimestampTests {

    #region 1. Constants & Basic State

    [Fact]
    public void Epoch_ShouldBeZero() {
        Assert.Equal(0, UnixTimestamp.Epoch.Milliseconds);
    }

    [Fact]
    public void MinMaxValues_ShouldMatchInt64() {
        Assert.Equal(long.MinValue, UnixTimestamp.MinValue.Milliseconds);
        Assert.Equal(long.MaxValue, UnixTimestamp.MaxValue.Milliseconds);
    }

    [Fact]
    public void From_Long_ShouldStoreValue() {
        long val = 123456789;
        // Static factory method testi
        UnixTimestamp ts = UnixTimestamp.From(val);
        Assert.Equal(val, ts.Milliseconds);
    }

    [Fact]
    public void ImplicitOperator_ToLong_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.From(100);
        long val = ts; // Implicit cast
        Assert.Equal(100, val);
    }

    [Fact]
    public void ExplicitOperator_FromLong_ShouldWork() {
        long val = 500;
        UnixTimestamp ts = (UnixTimestamp)val; // Explicit cast
        Assert.Equal(500, ts.Milliseconds);
    }

    [Fact]
    public void Now_ShouldBeCloseToSystemTime() {
        long systemMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UnixTimestamp now = UnixTimestamp.Now;

        // Toleranslı kontrol (test çalışırken geçen süre için)
        long diff = Math.Abs(now.Milliseconds - systemMs);
        Assert.True(diff < 100, "UnixTimestamp.Now is too far from system time.");
    }

    #endregion

    #region 2. DateTime & DateTimeOffset Conversions

    [Fact]
    public void From_DateTimeOffset_ShouldConvertCorrectly() {
        // 2023-01-01 00:00:00 UTC
        DateTimeOffset dto = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        UnixTimestamp ts = UnixTimestamp.From(dto);

        Assert.Equal(dto.ToUnixTimeMilliseconds(), ts.Milliseconds);
    }

    [Fact]
    public void From_DateTime_Utc_ShouldConvertCorrectly() {
        DateTime dt = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        UnixTimestamp ts = UnixTimestamp.From(dt);

        // DateTimeOffset conversion for verification
        long expected = new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        Assert.Equal(expected, ts.Milliseconds);
    }

    [Fact]
    public void From_DateTime_Local_ShouldConvertToUtc() {
        // Local time
        DateTime dtLocal = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Local);
        UnixTimestamp ts = UnixTimestamp.From(dtLocal);

        // Verification: Convert local back to UTC manually and check ms
        long expected = new DateTimeOffset(dtLocal.ToUniversalTime()).ToUnixTimeMilliseconds();
        Assert.Equal(expected, ts.Milliseconds);
    }

    [Fact]
    public void From_DateTime_Unspecified_ShouldTreatAsUtc() {
        // Unspecified kind -> Assumed UTC in your logic
        DateTime dtUnspec = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        UnixTimestamp ts = UnixTimestamp.From(dtUnspec);

        // Should match the UTC equivalent
        DateTime dtUtc = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long expected = new DateTimeOffset(dtUtc).ToUnixTimeMilliseconds();

        Assert.Equal(expected, ts.Milliseconds);
    }

    [Fact]
    public void ToDateTimeUtc_ShouldReturnUtcKind() {
        UnixTimestamp ts = UnixTimestamp.From(1672531200000); // 2023-01-01
        DateTime dt = ts.ToDateTimeUtc();

        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(1672531200000, new DateTimeOffset(dt).ToUnixTimeMilliseconds());
    }

    [Fact]
    public void ToDateTimeLocal_ShouldReturnLocalKind() {
        UnixTimestamp ts = UnixTimestamp.From(1672531200000);
        DateTime dt = ts.ToDateTimeLocal();

        Assert.Equal(DateTimeKind.Local, dt.Kind);
    }

    [Fact]
    public void ToDateTimeOffset_ShouldReturnOffsetZero() {
        UnixTimestamp ts = UnixTimestamp.From(1000);
        DateTimeOffset dto = ts.ToDateTimeOffset();

        Assert.Equal(TimeSpan.Zero, dto.Offset);
        Assert.Equal(1000, dto.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Cast_Operators_DateTimeOffset() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        // Truncate to ms precision because UnixTimestamp doesn't store ticks
        now = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());

        UnixTimestamp ts = (UnixTimestamp)now; // Explicit
        DateTimeOffset back = ts;              // Implicit

        Assert.Equal(now, back);
    }

    #endregion

    #region 3. Math Operators

    [Fact]
    public void Operator_Plus_TimeSpan_ShouldAdd() {
        UnixTimestamp start = UnixTimestamp.From(1000);
        UnixTimestamp result = start + TimeSpan.FromMilliseconds(500);

        Assert.Equal(1500, result.Milliseconds);
    }

    [Fact]
    public void Operator_Minus_TimeSpan_ShouldSubtract() {
        UnixTimestamp start = UnixTimestamp.From(1000);
        UnixTimestamp result = start - TimeSpan.FromMilliseconds(500);

        Assert.Equal(500, result.Milliseconds);
    }

    [Fact]
    public void Operator_Minus_UnixTimestamp_ShouldReturnTimeSpan() {
        UnixTimestamp end = UnixTimestamp.From(1500);
        UnixTimestamp start = UnixTimestamp.From(1000);

        TimeSpan diff = end - start;

        Assert.Equal(500, diff.TotalMilliseconds);
    }

    #endregion

    #region 4. Comparison & Equality

    [Fact]
    public void Comparison_Operators_ShouldWork() {
        UnixTimestamp small = UnixTimestamp.From(100);
        UnixTimestamp big = UnixTimestamp.From(200);
        UnixTimestamp small2 = UnixTimestamp.From(100);

        Assert.True(small < big);
        Assert.True(big > small);
        Assert.True(small <= small2);
        Assert.True(small >= small2);
    }

    [Fact]
    public void Equality_With_UnixTimestamp() {
        UnixTimestamp t1 = UnixTimestamp.From(123);
        UnixTimestamp t2 = UnixTimestamp.From(123);
        UnixTimestamp t3 = UnixTimestamp.From(456);

        Assert.True(t1 == t2);
        Assert.False(t1 == t3);
        Assert.True(t1 != t3);
        Assert.True(t1.Equals(t2));
    }

    [Fact]
    public void Equality_With_Long() {
        UnixTimestamp t1 = UnixTimestamp.From(1000);

        Assert.Equal(1000L, t1);
        Assert.NotEqual(2000L, t1);
    }

    [Fact]
    public void CompareTo_ShouldSortCorrectly() {
        List<UnixTimestamp> list = [
            UnixTimestamp.From(300),
            UnixTimestamp.From(100),
            UnixTimestamp.From(200)
        ];

        list.Sort();

        Assert.Equal(100, list[0].Milliseconds);
        Assert.Equal(200, list[1].Milliseconds);
        Assert.Equal(300, list[2].Milliseconds);
    }

    #endregion

    #region 5. Parsing (String & Char Span) - Updated for Explicit Interfaces

    [Theory]
    [InlineData("0", 0)]
    [InlineData("123456", 123456)]
    [InlineData("-1", -1)]
    public void Parse_String_Valid_ShouldWork(string input, long expected) {
        // Public convenience method (no provider needed)
        UnixTimestamp result = UnixTimestamp.Parse(input);
        Assert.Equal(expected, result.Milliseconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12.5")] // Integers only
    public void Parse_String_Invalid_ShouldThrow(string? input) {
        if(input == null)
            Assert.ThrowsAny<ArgumentException>(() => UnixTimestamp.Parse(input!));
        else
            Assert.Throws<FormatException>(() => UnixTimestamp.Parse(input));
    }

    [Fact]
    public void TryParse_String_ShouldWork() {
        // Public convenience method
        Assert.True(UnixTimestamp.TryParse("100", out UnixTimestamp ts));
        Assert.Equal(100, ts.Milliseconds);

        Assert.False(UnixTimestamp.TryParse("invalid", out _));
        Assert.False(UnixTimestamp.TryParse(null, out _));
    }

    [Fact]
    public void TryParse_Span_ShouldWork() {
        ReadOnlySpan<char> span = "999".AsSpan();

        // Public convenience method
        Assert.True(UnixTimestamp.TryParse(span, out UnixTimestamp ts));
        Assert.Equal(999, ts.Milliseconds);
    }

    [Fact]
    public void Explicit_IParsable_ShouldWork() {
        // Test strict interface implementation
        string input = "555";
        UnixTimestamp result = ParseHelper<UnixTimestamp>(input, null);
        Assert.Equal(555, result.Milliseconds);
    }

    #endregion

    #region 6. Parsing (UTF-8) - Updated for Explicit Interfaces

    [Fact]
    public void Explicit_IUtf8SpanParsable_Parse_ShouldWork() {
        byte[] bytes = Encoding.UTF8.GetBytes("12345");

        // Test via helper that calls the explicit interface method
        UnixTimestamp ts = ParseUtf8Helper<UnixTimestamp>(bytes, null);
        Assert.Equal(12345, ts.Milliseconds);
    }

    [Fact]
    public void Explicit_IUtf8SpanParsable_Parse_Invalid_ShouldThrow() {
        byte[] bytes = Encoding.UTF8.GetBytes("abc");
        Assert.Throws<FormatException>(() => ParseUtf8Helper<UnixTimestamp>(bytes, null));
    }

    [Fact]
    public void Explicit_IUtf8SpanParsable_TryParse_ShouldWork() {
        byte[] valid = Encoding.UTF8.GetBytes("500");
        byte[] invalid = Encoding.UTF8.GetBytes("xyz");

        Assert.True(TryParseUtf8Helper<UnixTimestamp>(valid, null, out UnixTimestamp t1));
        Assert.Equal(500, t1.Milliseconds);

        Assert.False(TryParseUtf8Helper<UnixTimestamp>(invalid, null, out _));
    }

    #endregion

    #region 7. Formatting - Updated for Explicit Interfaces

    [Fact]
    public void ToString_ShouldReturnNumberString() {
        UnixTimestamp ts = UnixTimestamp.From(12345);
        Assert.Equal("12345", ts.ToString());
    }

    [Fact]
    public void Explicit_ISpanFormattable_TryFormat_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.From(123);
        Span<char> buffer = stackalloc char[10];

        // Call explicit interface implementation via casting
        bool success = ((ISpanFormattable)ts).TryFormat(buffer, out int written, default, null);

        Assert.True(success);
        Assert.Equal("123", buffer[..written].ToString());
    }

    [Fact]
    public void Explicit_IUtf8SpanFormattable_TryFormat_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.From(987);
        Span<byte> buffer = stackalloc byte[10];

        // Call explicit interface implementation via casting
        bool success = ((IUtf8SpanFormattable)ts).TryFormat(buffer, out int written, default, null);

        Assert.True(success);
        string result = Encoding.UTF8.GetString(buffer[..written]);
        Assert.Equal("987", result);
    }

    #endregion

    #region 8. JSON Serialization

    private class TestModel {
        public UnixTimestamp Ts { get; set; }
    }

    [Fact]
    public void Json_Serialize_ShouldWriteNumber() {
        TestModel model = new() { Ts = UnixTimestamp.From(123456) };
        string json = JsonSerializer.Serialize(model);

        // Expected format: {"Ts":123456} (no quotes around number)
        Assert.Contains("\"Ts\":123456", json);
    }

    [Fact]
    public void Json_Deserialize_Number_ShouldWork() {
        string json = "{\"Ts\": 999}";
        TestModel? model = JsonSerializer.Deserialize<TestModel>(json);

        Assert.Equal(999, model!.Ts.Milliseconds);
    }

    [Fact]
    public void Json_Deserialize_String_ShouldWork_Fallback() {
        // Some APIs send numbers as strings ("999"), converter should handle this
        string json = "{\"Ts\": \"888\"}";
        TestModel? model = JsonSerializer.Deserialize<TestModel>(json);

        Assert.Equal(888, model!.Ts.Milliseconds);
    }

    [Fact]
    public void Json_Deserialize_Invalid_ShouldThrow() {
        string json = "{\"Ts\": \"abc\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestModel>(json));
    }

    [Fact]
    public void Json_DictionaryKey_ShouldWork() {
        // Tests ReadAsPropertyName and WriteAsPropertyName
        Dictionary<UnixTimestamp, string> dict = new() {
            { UnixTimestamp.From(100), "Value1" }
        };

        string json = JsonSerializer.Serialize(dict);
        // Keys in JSON are always strings: {"100":"Value1"}
        Assert.Contains("\"100\":", json);

        Dictionary<UnixTimestamp, string>? deserialized = JsonSerializer.Deserialize<Dictionary<UnixTimestamp, string>>(json);
        Assert.NotNull(deserialized);
        Assert.True(deserialized.ContainsKey(UnixTimestamp.From(100)));
        Assert.Equal("Value1", deserialized[UnixTimestamp.From(100)]);
    }

    #endregion

    #region 9. Helpers for Explicit Interfaces

    // Helper methods to access static abstract interface members which cannot be called directly on the struct
    // when implemented explicitly.

    private static T ParseHelper<T>(string s, IFormatProvider? provider) where T : IParsable<T> {
        return T.Parse(s, provider);
    }

    private static T ParseUtf8Helper<T>(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) where T : IUtf8SpanParsable<T> {
        return T.Parse(utf8Text, provider);
    }

    private static bool TryParseUtf8Helper<T>(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out T? result) where T : IUtf8SpanParsable<T> {
        return T.TryParse(utf8Text, provider, out result);
    }

    #endregion
}