using System.Text;
using System.Text.Json;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

public sealed class WebhookEndpointIdTests {
    [Fact]
    public void Constructor_SetsValue_WhenValidString() {
        WebhookEndpointId id = new("endpoint-123");
        Assert.Equal("endpoint-123", id.Value);
        Assert.Equal("endpoint-123", id.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Constructor_ThrowsWhenValueIsNullOrWhiteSpace(string? value) {
        Assert.ThrowsAny<ArgumentException>(() => new WebhookEndpointId(value!));
    }

    [Fact]
    public void Parse_And_TryParse_WorkWithStringsAndSpans() {
        WebhookEndpointId parsed1 = WebhookEndpointId.Parse("ep-1");
        Assert.Equal("ep-1", parsed1.Value);

        WebhookEndpointId parsed2 = WebhookEndpointId.Parse("ep-2".AsSpan());
        Assert.Equal("ep-2", parsed2.Value);

        Assert.True(WebhookEndpointId.TryParse("ep-3", out WebhookEndpointId try1));
        Assert.Equal("ep-3", try1.Value);

        Assert.True(WebhookEndpointId.TryParse("ep-4".AsSpan(), out WebhookEndpointId try2));
        Assert.Equal("ep-4", try2.Value);

        Assert.False(WebhookEndpointId.TryParse((string?)null, out _));
        Assert.False(WebhookEndpointId.TryParse("   ", out _));
        Assert.False(WebhookEndpointId.TryParse(ReadOnlySpan<char>.Empty, out _));
    }

    [Fact]
    public void Equality_ComparesUnderlyingValuesCorrectly() {
        WebhookEndpointId id1 = new("same-id");
        WebhookEndpointId id2 = new("same-id");
        WebhookEndpointId id3 = new("different-id");

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.True(id1 == id2);
        Assert.False(id1 == id3);
        Assert.False(id1 != id2);
        Assert.True(id1 != id3);
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void Comparers_HandleCaseSensitivityCorrectly() {
        WebhookEndpointId lower = new("endpoint-abc");
        WebhookEndpointId upper = new("ENDPOINT-ABC");

        // Ordinal Comparer
        Assert.False(WebhookEndpointId.OrdinalComparer.Equals(lower, upper));
        Assert.NotEqual(WebhookEndpointId.OrdinalComparer.GetHashCode(lower), WebhookEndpointId.OrdinalComparer.GetHashCode(upper));

        // OrdinalIgnoreCase Comparer
        Assert.True(WebhookEndpointId.OrdinalIgnoreCaseComparer.Equals(lower, upper));
        Assert.Equal(WebhookEndpointId.OrdinalIgnoreCaseComparer.GetHashCode(lower), WebhookEndpointId.OrdinalIgnoreCaseComparer.GetHashCode(upper));

        Dictionary<WebhookEndpointId, string> dict = new(WebhookEndpointId.OrdinalIgnoreCaseComparer) {
            [lower] = "registered"
        };
        Assert.True(dict.ContainsKey(upper));
        Assert.Equal("registered", dict[upper]);
    }

    [Fact]
    public void CompareTo_SortsCorrectly() {
        WebhookEndpointId idA = new("a");
        WebhookEndpointId idB = new("b");

        Assert.True(idA.CompareTo(idB) < 0);
        Assert.True(idB.CompareTo(idA) > 0);
        Assert.Equal(0, idA.CompareTo(new WebhookEndpointId("a")));
        Assert.Equal(1, idA.CompareTo(null));

        List<WebhookEndpointId> list = [idB, idA];
        list.Sort();
        Assert.Equal("a", list[0].Value);
        Assert.Equal("b", list[1].Value);
    }

    [Fact]
    public void Comparers_SupportAlternateSpanLookups_WithoutAllocation() {
        WebhookEndpointId id = new("endpoint-xyz");
        Dictionary<WebhookEndpointId, string> dict = new(WebhookEndpointId.OrdinalIgnoreCaseComparer) {
            [id] = "active"
        };

        Dictionary<WebhookEndpointId, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            dict.GetAlternateLookup<ReadOnlySpan<char>>();

        ReadOnlySpan<char> spanKey = "ENDPOINT-XYZ".AsSpan();
        Assert.True(lookup.TryGetValue(spanKey, out string? value));
        Assert.Equal("active", value);
    }

    [Fact]
    public void TryFormat_And_Utf8SpanParsing_WorkCorrectly() {
        WebhookEndpointId id = new("tenant-100");

        // Char Span Formatting
        Span<char> charBuf = stackalloc char[32];
        Assert.True(id.TryFormat(charBuf, out int charsWritten));
        Assert.Equal("tenant-100", charBuf[..charsWritten].ToString());

        // UTF-8 Byte Span Formatting
        Span<byte> utf8Buf = stackalloc byte[32];
        Assert.True(id.TryFormat(utf8Buf, out int bytesWritten));
        Assert.Equal("tenant-100", Encoding.UTF8.GetString(utf8Buf[..bytesWritten]));

        // UTF-8 Byte Span Parsing
        byte[] utf8Bytes = "tenant-100"u8.ToArray();
        Assert.True(WebhookEndpointId.TryParse(utf8Bytes, out WebhookEndpointId parsedFromUtf8));
        Assert.Equal(id, parsedFromUtf8);

        WebhookEndpointId parsedDirect = WebhookEndpointId.Parse(utf8Bytes);
        Assert.Equal(id, parsedDirect);
    }

    [Fact]
    public void SystemTextJson_SerializesAsFlatString_AndWorksAsDictionaryKey() {
        WebhookEndpointId id = new("acme-webhook");

        // 1. Standalone value serialization
        string json = JsonSerializer.Serialize(id);
        Assert.Equal("\"acme-webhook\"", json);

        WebhookEndpointId deserialized = JsonSerializer.Deserialize<WebhookEndpointId>(json);
        Assert.Equal(id, deserialized);

        // 2. Dictionary key serialization
        Dictionary<WebhookEndpointId, int> dict = new() {
            [id] = 42
        };
        string dictJson = JsonSerializer.Serialize(dict);
        Assert.Contains("\"acme-webhook\":42", dictJson);

        Dictionary<WebhookEndpointId, int>? deserializedDict = JsonSerializer.Deserialize<Dictionary<WebhookEndpointId, int>>(dictJson);
        Assert.NotNull(deserializedDict);
        Assert.True(deserializedDict.ContainsKey(id));
        Assert.Equal(42, deserializedDict[id]);
    }
}
