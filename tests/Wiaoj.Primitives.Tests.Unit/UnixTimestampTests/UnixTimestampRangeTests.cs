using System.Text;

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
    public void Operators_ShouldThrow_WhenTheResultOverflows() {
        UnixTimestamp nearMax = UnixTimestamp.FromMilliseconds(long.MaxValue - 1);
        UnixTimestamp nearMin = UnixTimestamp.FromMilliseconds(long.MinValue + 1);

        Assert.Throws<OverflowException>(() => nearMax + TimeSpan.FromMilliseconds(2));
        Assert.Throws<OverflowException>(() => nearMin - TimeSpan.FromMilliseconds(2));
        Assert.Equal(long.MaxValue, (nearMax + TimeSpan.FromMilliseconds(1)).TotalMilliseconds);
        Assert.Equal(long.MinValue, (nearMin - TimeSpan.FromMilliseconds(1)).TotalMilliseconds);
    }
}
