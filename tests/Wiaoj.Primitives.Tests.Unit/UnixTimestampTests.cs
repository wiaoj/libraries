using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;

public sealed class UnixTimestampTests {

    #region 1. Constants & Basic State

    [Fact]
    public void Epoch_ShouldBeZero() {
        Assert.Equal(0, UnixTimestamp.Epoch.TotalMilliseconds);
    }

    [Fact]
    public void MinMaxValues_ShouldMatchDateTimeOffsetLimits() {
        // Kodda artık DateTimeOffset sınırlarını (0001-9999) kullandığımız için 
        // testi bu sınırlara göre güncelledik.
        Assert.Equal(DateTimeOffset.MinValue.ToUnixTimeMilliseconds(), UnixTimestamp.MinValue.TotalMilliseconds);
        Assert.Equal(DateTimeOffset.MaxValue.ToUnixTimeMilliseconds(), UnixTimestamp.MaxValue.TotalMilliseconds);
    }

    [Fact]
    public void TotalSeconds_ShouldReturnCorrectValue() {
        var ts = UnixTimestamp.FromMilliseconds(1500);
        Assert.Equal(1, ts.TotalSeconds);
    }

    [Fact]
    public void From_Long_ShouldStoreValue() {
        long val = 123456789;
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(val);
        Assert.Equal(val, ts.TotalMilliseconds);
    }

    [Fact]
    public void From_Seconds_ShouldMultiplyCorrectly() {
        UnixTimestamp ts = UnixTimestamp.FromSeconds(10);
        Assert.Equal(10000, ts.TotalMilliseconds);
    }

    [Fact]
    public void ImplicitOperator_ToLong_ShouldWork() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(100);
        long val = ts;
        Assert.Equal(100, val);
    }

    [Fact]
    public void ExplicitOperator_FromLong_ShouldWork() {
        long val = 500;
        UnixTimestamp ts = (UnixTimestamp)val;
        Assert.Equal(500, ts.TotalMilliseconds);
    }

    [Fact]
    public void Now_ShouldBeCloseToSystemTime() {
        long systemMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UnixTimestamp now = UnixTimestamp.Now;
        long diff = Math.Abs(now.TotalMilliseconds - systemMs);
        Assert.True(diff < 500, "UnixTimestamp.Now is too far from system time.");
    }

    [Fact]
    public void From_TimeProvider_ShouldWork() {
        var ts = UnixTimestamp.From(TimeProvider.System);
        Assert.True(ts.TotalMilliseconds > 0);
    }

    #endregion

    #region 2. DateTime & DateTimeOffset Conversions

    [Fact]
    public void From_DateTimeOffset_ShouldConvertCorrectly() {
        DateTimeOffset dto = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        UnixTimestamp ts = UnixTimestamp.From(dto);
        Assert.Equal(dto.ToUnixTimeMilliseconds(), ts.TotalMilliseconds);
    }

    [Fact]
    public void From_DateTime_Utc_ShouldConvertCorrectly() {
        DateTime dt = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        UnixTimestamp ts = UnixTimestamp.From(dt);
        Assert.Equal(new DateTimeOffset(dt).ToUnixTimeMilliseconds(), ts.TotalMilliseconds);
    }

    [Fact]
    public void From_DateTime_Local_ShouldConvertToUtc() {
        DateTime dtLocal = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Local);
        UnixTimestamp ts = UnixTimestamp.From(dtLocal);
        long expected = new DateTimeOffset(dtLocal.ToUniversalTime()).ToUnixTimeMilliseconds();
        Assert.Equal(expected, ts.TotalMilliseconds);
    }

    [Fact]
    public void From_DateTime_Unspecified_ShouldTreatAsUtc() {
        DateTime dtUnspec = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        UnixTimestamp ts = UnixTimestamp.From(dtUnspec);
        Assert.Equal(1672531200000, ts.TotalMilliseconds);
    }

    [Fact]
    public void ToDateTimeUtc_ShouldReturnUtcKind() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1672531200000);
        DateTime dt = ts.ToDateTimeUtc();
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
    }

    [Fact]
    public void ToDateTimeLocal_ShouldReturnLocalKind() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1672531200000);
        DateTime dt = ts.ToDateTimeLocal();
        Assert.Equal(DateTimeKind.Local, dt.Kind);
    }

    [Fact]
    public void ToDateTimeOffset_ShouldReturnOffsetZero() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1000);
        DateTimeOffset dto = ts.ToDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, dto.Offset);
    }

    [Fact]
    public void Cast_Operators_DateTimeOffset() {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        UnixTimestamp ts = (UnixTimestamp)now;
        DateTimeOffset back = ts;
        Assert.Equal(now, back);
    }

    #endregion

    #region 3. Math Operators

    [Fact]
    public void Operator_Plus_TimeSpan_ShouldAdd() {
        UnixTimestamp start = UnixTimestamp.FromMilliseconds(1000);
        UnixTimestamp result = start + TimeSpan.FromMilliseconds(500);
        Assert.Equal(1500, result.TotalMilliseconds);
    }

    [Fact]
    public void Operator_Minus_TimeSpan_ShouldSubtract() {
        UnixTimestamp start = UnixTimestamp.FromMilliseconds(1000);
        UnixTimestamp result = start - TimeSpan.FromMilliseconds(500);
        Assert.Equal(500, result.TotalMilliseconds);
    }

    [Fact]
    public void Operator_Minus_UnixTimestamp_ShouldReturnTimeSpan() {
        UnixTimestamp end = UnixTimestamp.FromMilliseconds(1500);
        UnixTimestamp start = UnixTimestamp.FromMilliseconds(1000);
        TimeSpan diff = end - start;
        Assert.Equal(500, diff.TotalMilliseconds);
    }

    #endregion

    #region 4. Comparison & Equality

    [Fact]
    public void Comparison_Operators_ShouldWork() {
        UnixTimestamp small = UnixTimestamp.FromMilliseconds(100);
        UnixTimestamp big = UnixTimestamp.FromMilliseconds(200);
        Assert.True(small < big);
        Assert.True(big > small);
        Assert.True(small <= (UnixTimestamp)100);
        Assert.True(big >= (UnixTimestamp)200);
    }

    [Fact]
    public void Equality_With_UnixTimestamp() {
        UnixTimestamp t1 = (UnixTimestamp)123;
        UnixTimestamp t2 = (UnixTimestamp)123;
        Assert.True(t1 == t2);
        Assert.False(t1 != t2);
        Assert.True(t1.Equals(t2));
    }

    [Fact]
    public void Equality_With_Long() {
        UnixTimestamp t1 = (UnixTimestamp)1000;
        Assert.True(t1 == 1000L);
        Assert.True(t1 != 2000L);
    }

    [Fact]
    public void CompareTo_ShouldSortCorrectly() {
        List<UnixTimestamp> list = [(UnixTimestamp)300, (UnixTimestamp)100, (UnixTimestamp)200];
        list.Sort();
        Assert.Equal(100, list[0].TotalMilliseconds);
        Assert.Equal(200, list[1].TotalMilliseconds);
        Assert.Equal(300, list[2].TotalMilliseconds);
    }

    [Fact]
    public void GetHashCode_ShouldBeEqualForSameValue() {
        Assert.Equal(((UnixTimestamp)100).GetHashCode(), ((UnixTimestamp)100).GetHashCode());
    }

    #endregion

    #region 5. Parsing (String & Char Span)

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
        Assert.False(UnixTimestamp.TryParse(null, out _));
    }

    [Fact]
    public void TryParse_Span_ShouldWork() {
        Assert.True(UnixTimestamp.TryParse("999".AsSpan(), out var ts) && ts == 999);
    }

    [Fact]
    public void Explicit_IParsable_ShouldWork() {
        Assert.Equal(555, ParseHelper<UnixTimestamp>("555").TotalMilliseconds);
    }

    [Fact]
    public void Explicit_ISpanParsable_ShouldWork() {
        Assert.Equal(777, ParseSpanHelper<UnixTimestamp>("777").TotalMilliseconds);
    }

    #endregion

    #region 6. Parsing (UTF-8)

    [Fact]
    public void Explicit_IUtf8SpanParsable_Parse_ShouldWork() {
        Assert.Equal(12345, ParseUtf8Helper<UnixTimestamp>("12345").TotalMilliseconds);
    }

    [Fact]
    public void Explicit_IUtf8SpanParsable_TryParse_ShouldWork() {
        byte[] valid = "500"u8.ToArray();
        Assert.True(TryParseUtf8Helper<UnixTimestamp>(valid, out var ts) && ts == 500);
    }

    #endregion

    #region 7. Formatting (ISO vs Raw vs Safe)

    [Fact]
    public void ToString_ShouldReturnIso8601ByDefault() {
        // Loglama felsefemize göre varsayılan ISO'dur.
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
    public void ToString_OutOfRange_ShouldFallbackToRawWithoutCrashing() {
        // DateTimeOffset sınırları dışındaki ekstrem değerler patlamaz, sayı döner.
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

    #endregion

    #region 8. JSON Serialization

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
        // Keyler ISO değil, "100" gibi rakam olmalı.
        var dict = new Dictionary<UnixTimestamp, string> { { (UnixTimestamp)100, "V1" } };
        string json = JsonSerializer.Serialize(dict);
        Assert.Contains("\"100\":", json);
        Assert.DoesNotContain("1970-01-01", json);
    }

    #endregion

    #region 9. Helpers (Interface Testers)

    private static T ParseHelper<T>(string s) where T : IParsable<T>
        => T.Parse(s, CultureInfo.InvariantCulture);

    private static T ParseSpanHelper<T>(string s) where T : ISpanParsable<T>
        => T.Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    private static T ParseUtf8Helper<T>(string s) where T : IUtf8SpanParsable<T>
        => T.Parse(Encoding.UTF8.GetBytes(s), CultureInfo.InvariantCulture);

    private static bool TryParseUtf8Helper<T>(byte[] b, out T res) where T : IUtf8SpanParsable<T>
        => T.TryParse(b, CultureInfo.InvariantCulture, out res!);

    #endregion
}