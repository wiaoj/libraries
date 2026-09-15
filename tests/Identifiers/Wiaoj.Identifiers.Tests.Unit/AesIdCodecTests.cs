using System.Security.Cryptography;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers.Tests.Unit;

/// <summary>
/// The AES codec hides the value, and reads back only text written under the same key, version and prefix: a forged,
/// altered or re-prefixed identifier is refused.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "AesIdCodec")]
public sealed class AesIdCodecTests {
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly AesIdCodec Codec = TestCodecs.Aes();

    [Fact]
    public void Should_Write_The_Prefix_The_Version_And_22_Base62_Characters() {
        string text = Codec.Encode("usr", new SnowflakeId(42));

        Assert.StartsWith("usr_1", text, StringComparison.Ordinal);
        Assert.Equal("usr_".Length + 1 + 22, text.Length);
        Assert.All(text["usr_1".Length..], c => Assert.Contains(c, Base62Alphabet));
        Assert.Equal(Codec.GetMaxEncodedLength("usr"), text.Length);
    }

    [Fact]
    public void Should_Keep_The_Format_Stable_For_A_Known_Key() {
        // Pins the construction (HKDF info strings, tag layout, byte order, base62): changing any of them would change
        // every identifier already issued, so a failure here means a breaking change. The expected text was computed
        // outside the library, from HKDF-SHA256, HMAC-SHA256 and AES-ECB in PowerShell.
        Assert.Equal(KnownAnswer, Codec.Encode("usr", new SnowflakeId(1234567890123456789)));
        Assert.True(Codec.TryDecode("usr", KnownAnswer, out SnowflakeId value));
        Assert.Equal(1234567890123456789, value.Value);
    }

    private const string KnownAnswer = "usr_11bNq7n40PwSW4duiTdFuw6";

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Should_Round_Trip_Edge_Values(long raw) {
        Assert.True(Codec.TryDecode("usr", Codec.Encode("usr", new SnowflakeId(raw)), out SnowflakeId value));
        Assert.Equal(raw, value.Value);
    }

    [Fact]
    public void Should_Round_Trip_Random_Values_Deterministically() {
        for(int i = 0; i < 2000; i++) {
            SnowflakeId value = new(Random.Shared.NextInt64(long.MinValue, long.MaxValue));
            string text = Codec.Encode("usr", value);

            Assert.Equal(text, Codec.Encode("usr", value));
            Assert.True(Codec.TryDecode("usr", text, out SnowflakeId decoded));
            Assert.Equal(value, decoded);
        }
    }

    [Fact]
    public void Should_Not_Reveal_Consecutive_Values() {
        // Plain base62 of consecutive values shares every character but the last; encrypted text shares no structure.
        string first = Codec.Encode("usr", new SnowflakeId(1000))["usr_1".Length..];
        string second = Codec.Encode("usr", new SnowflakeId(1001))["usr_1".Length..];

        int samePositions = first.Zip(second).Count(pair => pair.First == pair.Second);
        Assert.True(samePositions < 6, $"{samePositions} of 22 characters are unchanged between consecutive values.");
    }

    [Fact]
    public void Should_Refuse_An_Identifier_Moved_To_Another_Prefix() {
        string user = Codec.Encode("usr", new SnowflakeId(42));
        string asOrg = "org" + user["usr".Length..];

        Assert.False(Codec.TryDecode("org", asOrg, out _));
        Assert.NotEqual(Codec.Encode("org", new SnowflakeId(42))["org_".Length..], user["usr_".Length..]);
    }

    [Fact]
    public void Should_Refuse_Every_Single_Character_Change() {
        string text = Codec.Encode("usr", new SnowflakeId(987654321));

        for(int position = "usr_1".Length; position < text.Length; position++) {
            foreach(char replacement in Base62Alphabet) {
                if(replacement == text[position]) {
                    continue;
                }

                string altered = string.Concat(text.AsSpan(0, position), replacement.ToString(), text.AsSpan(position + 1));
                Assert.False(Codec.TryDecode("usr", altered, out _), $"'{altered}' was accepted.");
            }
        }
    }

    [Fact]
    public void Should_Refuse_Made_Up_Identifiers() {
        for(int i = 0; i < 20_000; i++) {
            string payload = RandomNumberGenerator.GetString(Base62Alphabet, 22);
            Assert.False(Codec.TryDecode("usr", "usr_1" + payload, out _));
        }
    }

    [Fact]
    public void Should_Refuse_Text_Written_Under_Another_Key() {
        AesIdCodec other = new(TestCodecs.OtherKey);
        string text = other.Encode("usr", new SnowflakeId(42));

        Assert.NotEqual(text, Codec.Encode("usr", new SnowflakeId(42)));
        Assert.False(Codec.TryDecode("usr", text, out _));
    }

    [Fact]
    public void Should_Refuse_Text_Written_Under_Another_Version() {
        AesIdCodec version2 = TestCodecs.Aes('2');
        string text = version2.Encode("usr", new SnowflakeId(42));

        Assert.StartsWith("usr_2", text, StringComparison.Ordinal);
        Assert.False(Codec.TryDecode("usr", text, out _));
        Assert.True(version2.TryDecode("usr", text, out _));
    }

    [Theory]
    // 22 characters above 2^128 - 1.
    [InlineData("usr_1zzzzzzzzzzzzzzzzzzzzzz")]
    // Wrong lengths.
    [InlineData("usr_1zzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("usr_10000000000000000000000")]
    [InlineData("usr_1")]
    [InlineData("usr_")]
    [InlineData("org_10000000000000000000000")]
    // Not base62 or not ASCII.
    [InlineData("usr_1000000000000000000000-")]
    [InlineData("usr_100000000000000000000é")]
    public void Should_Refuse_Malformed_Text(string text) {
        Assert.False(Codec.TryDecode("usr", text, out SnowflakeId value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void Should_Refuse_A_Second_Spelling_Of_A_Valid_Identifier_Above_2_To_The_128() {
        // 22 base62 digits reach past 2^128, so an unchecked read would wrap the value + 2^128 back onto a valid block —
        // a second string for the same identifier.
        string text = Codec.Encode("usr", new SnowflakeId(42));
        System.Numerics.BigInteger value = System.Numerics.BigInteger.Zero;
        foreach(char c in text["usr_1".Length..]) {
            value = (value * 62) + Base62Alphabet.IndexOf(c);
        }

        System.Numerics.BigInteger alias = value + System.Numerics.BigInteger.Pow(2, 128);
        Assert.True(alias < System.Numerics.BigInteger.Pow(62, 22));

        char[] digits = new char[22];
        for(int d = 21; d >= 0; d--) {
            digits[d] = Base62Alphabet[(int)(alias % 62)];
            alias /= 62;
        }

        Assert.False(Codec.TryDecode("usr", "usr_1" + new string(digits), out _));
    }

    [Fact]
    public void Should_Read_Utf8_Text() {
        string text = Codec.Encode("usr", new SnowflakeId(7));

        Assert.True(Codec.TryDecode("usr", System.Text.Encoding.ASCII.GetBytes(text), out SnowflakeId value));
        Assert.Equal(7, value.Value);
    }

    [Fact]
    public void Should_Be_Safe_To_Use_From_Many_Threads() {
        Parallel.For(0, 20_000, i => {
            SnowflakeId value = new(i * 7919L);
            string text = Codec.Encode("usr", value);

            Assert.True(Codec.TryDecode("usr", text, out SnowflakeId decoded));
            Assert.Equal(value, decoded);
        });
    }

    [Fact]
    public void Should_Report_A_Destination_Too_Small() {
        Span<char> buffer = stackalloc char[Codec.GetMaxEncodedLength("usr") - 1];

        Assert.False(Codec.TryEncode("usr", new SnowflakeId(1), buffer, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void Should_Refuse_A_Key_Shorter_Than_128_Bits() {
        Assert.Throws<ArgumentException>(() => new AesIdCodec(new byte[15]));
        _ = new AesIdCodec(new byte[16]);
    }

    [Theory]
    [InlineData('_')]
    [InlineData('-')]
    [InlineData(' ')]
    [InlineData('é')]
    public void Should_Refuse_A_Version_That_Is_Not_An_Ascii_Letter_Or_Digit(char version) {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AesIdCodec(TestCodecs.Key, version));
    }

    [Fact]
    public void Should_Match_The_Documented_Construction_For_Any_Key_And_Prefix() {
        // An independent implementation of the construction the codec documents, checked against it for random inputs.
        for(int i = 0; i < 200; i++) {
            byte[] key = RandomNumberGenerator.GetBytes(16 + (i % 32));
            string prefix = i % 2 == 0 ? "usr" : "api_key";
            long raw = Random.Shared.NextInt64(long.MinValue, long.MaxValue);

            Assert.Equal(Reference(key, prefix, raw), new AesIdCodec(key, 'k').Encode(prefix, new SnowflakeId(raw)));
        }
    }

    private static string Reference(byte[] key, string prefix, long value) {
        byte[] aesKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, 16, [], "wiaoj.identifiers.aes.v1"u8.ToArray());
        byte[] tagKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, 32, [], "wiaoj.identifiers.tag.v1"u8.ToArray());

        byte[] block = new byte[16];
        HMACSHA256.HashData(tagKey, System.Text.Encoding.UTF8.GetBytes(prefix)).AsSpan(0, 8).CopyTo(block);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(block.AsSpan(8), value);

        using Aes aes = Aes.Create();
        aes.Key = aesKey;
        byte[] cipher = aes.EncryptEcb(block, PaddingMode.None);

        System.Numerics.BigInteger number = new(cipher, isUnsigned: true, isBigEndian: true);
        char[] digits = new char[22];
        for(int d = 21; d >= 0; d--) {
            digits[d] = Base62Alphabet[(int)(number % 62)];
            number /= 62;
        }

        return $"{prefix}_k{new string(digits)}";
    }
}