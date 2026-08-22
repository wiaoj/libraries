using System.Text;
using System.Text.Json;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

public sealed class WebhookSignatureTests {
    [Fact]
    public void Constructor_InitializesPropertiesAndHeaderValue_Correctly() {
        UnixTimestamp timestamp = UnixTimestamp.FromSeconds(1724190000);
        const string scheme = "v1";
        const string signatureHash = "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";

        WebhookSignature signature = new(timestamp, scheme, signatureHash);

        Assert.Equal(timestamp, signature.Timestamp);
        Assert.Equal(scheme, signature.Scheme);
        Assert.Equal(signatureHash, signature.Signature);
        Assert.Equal("t=1724190000,v1=4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", signature.HeaderValue);
        Assert.Equal(signature.HeaderValue, signature.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenSchemeIsNullOrWhiteSpace(string? scheme) {
        UnixTimestamp timestamp = UnixTimestamp.Now;
        Assert.ThrowsAny<ArgumentException>(() => new WebhookSignature(timestamp, scheme!, "sig123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenSignatureIsNullOrWhiteSpace(string? signature) {
        UnixTimestamp timestamp = UnixTimestamp.Now;
        Assert.ThrowsAny<ArgumentException>(() => new WebhookSignature(timestamp, "v1", signature!));
    }

    [Fact]
    public void Parse_And_TryParse_WorkWithCanonicalHeader() {
        const string header = "t=1724190000,v1=4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";

        WebhookSignature sig1 = WebhookSignature.Parse(header);
        Assert.Equal(1724190000, sig1.Timestamp.TotalSeconds);
        Assert.Equal("v1", sig1.Scheme);
        Assert.Equal("4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", sig1.Signature);

        WebhookSignature sig2 = WebhookSignature.Parse(header.AsSpan());
        Assert.Equal(sig1, sig2);

        Assert.True(WebhookSignature.TryParse(header, out WebhookSignature try1));
        Assert.Equal(sig1, try1);

        Assert.False(WebhookSignature.TryParse("invalid_format", out _));
        Assert.False(WebhookSignature.TryParse("t=invalid,v1=hash", out _));
        Assert.False(WebhookSignature.TryParse("t=1000", out _));
        Assert.False(WebhookSignature.TryParse((string?)null, out _));
    }

    [Fact]
    public void Equality_ComparesCorrectly() {
        UnixTimestamp timestamp1 = UnixTimestamp.FromSeconds(1000);
        UnixTimestamp timestamp2 = UnixTimestamp.FromSeconds(2000);

        WebhookSignature sig1 = new(timestamp1, "v1", "hash-a");
        WebhookSignature sig2 = new(timestamp1, "v1", "hash-a");
        WebhookSignature sig3 = new(timestamp2, "v1", "hash-a");
        WebhookSignature sig4 = new(timestamp1, "v2", "hash-a");
        WebhookSignature sig5 = new(timestamp1, "v1", "hash-b");

        Assert.Equal(sig1, sig2);
        Assert.True(sig1 == sig2);
        Assert.False(sig1 != sig2);
        Assert.Equal(sig1.GetHashCode(), sig2.GetHashCode());

        Assert.NotEqual(sig1, sig3);
        Assert.NotEqual(sig1, sig4);
        Assert.NotEqual(sig1, sig5);

        Assert.True(WebhookSignature.Comparer.Equals(sig1, sig2));
        Assert.False(WebhookSignature.Comparer.Equals(sig1, sig3));
        Assert.Equal(WebhookSignature.Comparer.GetHashCode(sig1), WebhookSignature.Comparer.GetHashCode(sig2));
    }

    [Fact]
    public void CompareTo_SortsByTimestampThenSchemeThenSignature() {
        WebhookSignature sig1 = new(UnixTimestamp.FromSeconds(1000), "v1", "hash1");
        WebhookSignature sig2 = new(UnixTimestamp.FromSeconds(2000), "v1", "hash1");
        WebhookSignature sig3 = new(UnixTimestamp.FromSeconds(1000), "v2", "hash1");

        Assert.True(sig1.CompareTo(sig2) < 0);
        Assert.True(sig2.CompareTo(sig1) > 0);
        Assert.True(sig1.CompareTo(sig3) < 0);
        Assert.Equal(1, sig1.CompareTo(null));
    }

    [Fact]
    public void Comparers_SupportAlternateSpanLookups_WithoutAllocation() {
        WebhookSignature sig = new(UnixTimestamp.FromSeconds(1700000000), "v1", "hash123");
        Dictionary<WebhookSignature, string> dict = new(WebhookSignature.Comparer) {
            [sig] = "valid"
        };

        Dictionary<WebhookSignature, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            dict.GetAlternateLookup<ReadOnlySpan<char>>();

        ReadOnlySpan<char> spanKey = "t=1700000000,v1=hash123".AsSpan();
        Assert.True(lookup.TryGetValue(spanKey, out string? value));
        Assert.Equal("valid", value);
    }

    [Fact]
    public void TryFormat_And_Utf8SpanParsing_WorkCorrectly() {
        WebhookSignature sig = new(UnixTimestamp.FromSeconds(1724190000), "v1", "abc456");

        // Char Span Formatting
        Span<char> charBuf = stackalloc char[64];
        Assert.True(sig.TryFormat(charBuf, out int charsWritten));
        Assert.Equal("t=1724190000,v1=abc456", charBuf[..charsWritten].ToString());

        // UTF-8 Byte Span Formatting
        Span<byte> utf8Buf = stackalloc byte[64];
        Assert.True(sig.TryFormat(utf8Buf, out int bytesWritten));
        Assert.Equal("t=1724190000,v1=abc456", Encoding.UTF8.GetString(utf8Buf[..bytesWritten]));

        // UTF-8 Byte Span Parsing
        byte[] utf8Bytes = "t=1724190000,v1=abc456"u8.ToArray();
        Assert.True(WebhookSignature.TryParse(utf8Bytes, out WebhookSignature parsedFromUtf8));
        Assert.Equal(sig, parsedFromUtf8);

        WebhookSignature parsedDirect = WebhookSignature.Parse(utf8Bytes);
        Assert.Equal(sig, parsedDirect);
    }

    [Fact]
    public void SystemTextJson_SerializesDirectlyToCanonicalHeaderString() {
        WebhookSignature sig = new(UnixTimestamp.FromSeconds(1700000000), "v1", "4f53cd");

        string json = JsonSerializer.Serialize(sig);
        Assert.Equal("\"t=1700000000,v1=4f53cd\"", json);

        WebhookSignature deserialized = JsonSerializer.Deserialize<WebhookSignature>(json);
        Assert.Equal(sig, deserialized);
    }
}
