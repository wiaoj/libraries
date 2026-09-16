using System.Text;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit.UnixTimestampTests;

/// <summary>
/// Seconds and parsed text are kept within <see cref="UnixTimestamp.MinValue"/>…<see cref="UnixTimestamp.MaxValue"/>, and
/// arithmetic that overflows throws instead of wrapping around.
/// </summary>
public sealed class UnixTimestampRangeTests {

    [Fact]
    public void MinMaxSeconds_ShouldMatchDateTimeOffsetLimits() {
        Assert.Equal(DateTimeOffset.MinValue.ToUnixTimeSeconds(), UnixTimestamp.MinSeconds);
        Assert.Equal(DateTimeOffset.MaxValue.ToUnixTimeSeconds(), UnixTimestamp.MaxSeconds);
    }

    [Fact]
    public void FromSeconds_ShouldAcceptTheLimits() {
        Assert.Equal(DateTimeOffset.MinValue, UnixTimestamp.FromSeconds(UnixTimestamp.MinSeconds).ToDateTimeOffset());
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(UnixTimestamp.MaxSeconds),
            UnixTimestamp.FromSeconds(UnixTimestamp.MaxSeconds).ToDateTimeOffset());
    }

    [Theory]
    [InlineData(UnixTimestamp.MinSeconds - 1)]
    [InlineData(UnixTimestamp.MaxSeconds + 1)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MaxValue / 1000 + 1)] // multiplying by 1000 would wrap around to a negative value
    public void FromSeconds_ShouldThrow_OutsideTheRange(long seconds) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.FromSeconds(seconds));
    }

    [Theory]
    [InlineData("-62135596800000")]
    [InlineData("253402300799999")]
    public void Parse_ShouldAcceptTheLimits(string text) {
        long expected = long.Parse(text);

        Assert.Equal(expected, UnixTimestamp.Parse(text).TotalMilliseconds);
        Assert.Equal(expected, UnixTimestamp.Parse(Encoding.UTF8.GetBytes(text)).TotalMilliseconds);
        Assert.True(UnixTimestamp.TryParse(text, out UnixTimestamp fromText) && fromText.TotalMilliseconds == expected);
        Assert.True(UnixTimestamp.TryParse(Encoding.UTF8.GetBytes(text), out UnixTimestamp fromUtf8) && fromUtf8.TotalMilliseconds == expected);
    }

    [Theory]
    [InlineData("-62135596800001")]
    [InlineData("253402300800000")]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    public void Parse_ShouldRefuse_OutsideTheRange(string text) {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);

        Assert.False(UnixTimestamp.TryParse(text, out UnixTimestamp fromText));
        Assert.Equal(default, fromText);
        Assert.False(UnixTimestamp.TryParse(utf8, out UnixTimestamp fromUtf8));
        Assert.Equal(default, fromUtf8);
        Assert.Throws<FormatException>(() => UnixTimestamp.Parse(text));
        Assert.Throws<FormatException>(() => UnixTimestamp.Parse(utf8));
    }

    [Fact]
    public void Operators_ShouldReachTheLimits_AndThrowBeyondThem() {
        UnixTimestamp justBelowMax = UnixTimestamp.FromMilliseconds(UnixTimestamp.MaxValue.TotalMilliseconds - 1);
        UnixTimestamp justAboveMin = UnixTimestamp.FromMilliseconds(UnixTimestamp.MinValue.TotalMilliseconds + 1);
        TimeSpan oneMs = TimeSpan.FromMilliseconds(1);

        Assert.Equal(UnixTimestamp.MaxValue, justBelowMax + oneMs);
        Assert.Equal(UnixTimestamp.MinValue, justAboveMin - oneMs);
        Assert.Equal(UnixTimestamp.MaxValue, justBelowMax.AddMilliseconds(1));
        Assert.Equal(UnixTimestamp.MinValue, justAboveMin.AddMilliseconds(-1));

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MaxValue + oneMs);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MinValue - oneMs);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MaxValue.AddMilliseconds(1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MinValue.AddMilliseconds(-1));
    }

    [Fact]
    public void Operators_ShouldThrow_InsteadOfWrappingAround() {
        // Subtracting TimeSpan.MinValue negates past long.MaxValue; adding long.MaxValue wraps a long sum.
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MaxValue + TimeSpan.MaxValue);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MinValue + TimeSpan.MinValue);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MaxValue - TimeSpan.MinValue);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MaxValue.AddMilliseconds(long.MaxValue));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.MinValue.AddMilliseconds(long.MinValue));
    }

    [Fact]
    public void FromMilliseconds_And_Cast_ShouldAcceptTheLimits() {
        Assert.Equal(UnixTimestamp.MinValue, UnixTimestamp.FromMilliseconds(-62135596800000));
        Assert.Equal(UnixTimestamp.MaxValue, UnixTimestamp.FromMilliseconds(253402300799999));
        Assert.Equal(UnixTimestamp.MinValue, (UnixTimestamp)(-62135596800000));
        Assert.Equal(UnixTimestamp.MaxValue, (UnixTimestamp)253402300799999);
    }

    [Theory]
    [InlineData(-62135596800001)]
    [InlineData(253402300800000)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void FromMilliseconds_And_Cast_ShouldThrow_OutsideTheRange(long milliseconds) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => UnixTimestamp.FromMilliseconds(milliseconds));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => (UnixTimestamp)milliseconds);
    }

    [Theory]
    [InlineData(-62135596800000, true)]
    [InlineData(253402300799999, true)]
    [InlineData(0, true)]
    [InlineData(-62135596800001, false)]
    [InlineData(253402300800000, false)]
    [InlineData(long.MaxValue, false)]
    public void TryFromMilliseconds_ShouldMatchTheRange(long milliseconds, bool expected) {
        Assert.Equal(expected, UnixTimestamp.TryFromMilliseconds(milliseconds, out UnixTimestamp result));
        Assert.Equal(expected ? milliseconds : 0, result.TotalMilliseconds);
    }

    [Theory]
    [InlineData(UnixTimestamp.MinSeconds, true)]
    [InlineData(UnixTimestamp.MaxSeconds, true)]
    [InlineData(1_700_000_000, true)]
    [InlineData(UnixTimestamp.MinSeconds - 1, false)]
    [InlineData(UnixTimestamp.MaxSeconds + 1, false)]
    [InlineData(long.MaxValue / 1000 + 1, false)]
    public void TryFromSeconds_ShouldMatchTheRange(long seconds, bool expected) {
        Assert.Equal(expected, UnixTimestamp.TryFromSeconds(seconds, out UnixTimestamp result));
        Assert.Equal(expected ? seconds * 1000 : 0, result.TotalMilliseconds);
    }

    [Theory]
    [InlineData("253402300800000")]
    [InlineData("\"253402300800000\"")]
    [InlineData("-62135596800001")]
    [InlineData("9223372036854775807")]
    public void Json_ShouldRefuse_OutsideTheRange(string json) {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UnixTimestamp>(json));
    }

    [Fact]
    public void Json_ShouldRefuse_OutsideTheRange_AsDictionaryKeyAndInARange() {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<UnixTimestamp, int>>("""{"253402300800000":1}"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Range<UnixTimestamp>>("""{"Min":0,"Max":253402300800000}"""));
    }

    [Fact]
    public void Json_ShouldAcceptTheLimits() {
        Assert.Equal(UnixTimestamp.MaxValue, JsonSerializer.Deserialize<UnixTimestamp>("253402300799999"));
        Assert.Equal(UnixTimestamp.MinValue, JsonSerializer.Deserialize<UnixTimestamp>("\"-62135596800000\""));
        Assert.Equal(
            UnixTimestamp.MaxValue,
            JsonSerializer.Deserialize<Dictionary<UnixTimestamp, int>>("""{"253402300799999":1}""")!.Keys.Single());
        Assert.Equal(
            new Range<UnixTimestamp>(UnixTimestamp.MinValue, UnixTimestamp.MaxValue),
            JsonSerializer.Deserialize<Range<UnixTimestamp>>("""{"Min":-62135596800000,"Max":253402300799999}"""));
    }
}
