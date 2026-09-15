using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers.Tests.Unit;

/// <summary>The plain codec writes the value in canonical base62 and reads back exactly that spelling.</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "PlainIdCodec")]
public sealed class PlainIdCodecTests {
    private static readonly PlainIdCodec Codec = PlainIdCodec.Instance;

    [Theory]
    [InlineData(0L, "usr_0")]
    [InlineData(1L, "usr_1")]
    [InlineData(61L, "usr_z")]
    [InlineData(62L, "usr_10")]
    [InlineData(3843L, "usr_zz")]
    [InlineData(long.MaxValue, "usr_AzL8n0Y58m7")]
    [InlineData(-1L, "usr_LygHa16AHYF")]
    public void Should_Write_The_Value_In_Canonical_Base62(long value, string expected) {
        Assert.Equal(expected, Codec.Encode("usr", new SnowflakeId(value)));

        Assert.True(Codec.TryDecode("usr", expected, out SnowflakeId decoded));
        Assert.Equal(value, decoded.Value);
    }

    [Fact]
    public void Should_Round_Trip_Generated_Values() {
        for(int i = 0; i < 1000; i++) {
            SnowflakeId value = new(Random.Shared.NextInt64(long.MinValue, long.MaxValue));

            Assert.True(Codec.TryDecode("usr", Codec.Encode("usr", value), out SnowflakeId decoded));
            Assert.Equal(value, decoded);
        }
    }

    [Theory]
    // Another spelling of a valid value: leading zeros.
    [InlineData("usr_01")]
    [InlineData("usr_00")]
    // Larger than 2^64 - 1.
    [InlineData("usr_LygHa16AHYG")]
    [InlineData("usr_zzzzzzzzzzz")]
    [InlineData("usr_100000000000")]
    // Wrong or missing prefix and separator.
    [InlineData("org_1")]
    [InlineData("USR_1")]
    [InlineData("usr1")]
    [InlineData("usr-1")]
    [InlineData("usr_")]
    [InlineData("usr")]
    [InlineData("")]
    [InlineData("_1")]
    // A longer prefix that starts with this one.
    [InlineData("usrx_1")]
    // Not base62.
    [InlineData("usr_1-2")]
    [InlineData("usr_ 1")]
    [InlineData("usr_1_")]
    public void Should_Refuse_Text_That_Is_Not_Exactly_What_It_Writes(string text) {
        Assert.False(Codec.TryDecode("usr", text, out SnowflakeId value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void Should_Refuse_A_Prefix_That_Is_Only_A_Start_Of_The_Text_Prefix() {
        Assert.False(Codec.TryDecode("us", "usr_1", out _));
    }

    [Fact]
    public void Should_Report_A_Destination_Too_Small() {
        Span<char> small = stackalloc char["usr_zz".Length - 1];

        Assert.False(Codec.TryEncode("usr", new SnowflakeId(3843), small, out int written));
        Assert.Equal(0, written);

        Span<char> exact = stackalloc char["usr_zz".Length];
        Assert.True(Codec.TryEncode("usr", new SnowflakeId(3843), exact, out written));
        Assert.Equal("usr_zz", exact[..written].ToString());
    }

    [Fact]
    public void Should_Never_Write_More_Than_Its_Maximum_Length() {
        Assert.Equal("usr_".Length + 11, Codec.GetMaxEncodedLength("usr"));
        Assert.Equal(Codec.GetMaxEncodedLength("usr"), Codec.Encode("usr", new SnowflakeId(-1)).Length);
    }

    /// <summary>Accepts any text and records it, to see what a codec is given.</summary>
    private sealed class RecordingCodec : IdCodec {
        public string? Seen { get; private set; }

        public override int GetMaxEncodedLength(string prefix) => 64;

        public override bool TryEncode(string prefix, SnowflakeId value, Span<char> destination, out int charsWritten) {
            charsWritten = 0;
            return false;
        }

        public override bool TryDecode(string prefix, ReadOnlySpan<char> text, out SnowflakeId value) {
            this.Seen = text.ToString();
            value = default;
            return true;
        }
    }

    [Fact]
    public void Should_Not_Hand_Non_Ascii_Utf8_To_A_Codec_As_Latin1_Characters() {
        RecordingCodec codec = new();

        Assert.False(codec.TryDecode("usr", "usr_é"u8, out _));
        Assert.Null(codec.Seen);

        Assert.True(codec.TryDecode("usr", "usr_e"u8, out _));
        Assert.Equal("usr_e", codec.Seen);
    }

    [Fact]
    public void Should_Read_Utf8_Text() {
        Assert.True(Codec.TryDecode("usr", "usr_zz"u8, out SnowflakeId value));
        Assert.Equal(3843, value.Value);

        Assert.False(Codec.TryDecode("usr", "usr_zé"u8, out _));
        Assert.False(Codec.TryDecode("usr", "usr_1234567890123"u8, out _));
    }
}
