using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Wiaoj.Webhooks.Tests.Unit.ValueObjects;

public sealed class WebhookJobIdTests {
    [Fact]
    public void NewJobId_GeneratesTimeOrderedPrefixedId() {
        WebhookJobId id1 = WebhookJobId.NewJobId();
        WebhookJobId id2 = WebhookJobId.NewJobId();

        Assert.StartsWith("job_", id1.Value);
        Assert.StartsWith("job_", id2.Value);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Parse_String_Succeeds_WhenValid() {
        WebhookJobId id = WebhookJobId.Parse("job_12345");
        Assert.Equal("job_12345", id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_String_Throws_WhenInvalid(string input) {
        Assert.ThrowsAny<ArgumentException>(() => WebhookJobId.Parse(input));
    }

    [Fact]
    public void Parse_Span_Succeeds_WhenValid() {
        ReadOnlySpan<char> span = "job_span_test".AsSpan();
        WebhookJobId id = WebhookJobId.Parse(span);
        Assert.Equal("job_span_test", id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Span_Throws_WhenInvalid(string input) {
        Assert.ThrowsAny<ArgumentException>(() => WebhookJobId.Parse(input.AsSpan()));
    }

    [Fact]
    public void Parse_Utf8_Succeeds_WhenValid() {
        byte[] bytes = Encoding.UTF8.GetBytes("job_utf8_test");
        WebhookJobId id = WebhookJobId.Parse(bytes.AsSpan());
        Assert.Equal("job_utf8_test", id.Value);
    }

    [Fact]
    public void Parse_Utf8_Throws_WhenInvalid() {
        Assert.ThrowsAny<ArgumentException>(() => WebhookJobId.Parse(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void TryParse_String_ReturnsTrue_WhenValid() {
        bool success = WebhookJobId.TryParse("job_valid", out WebhookJobId id);
        Assert.True(success);
        Assert.Equal("job_valid", id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_String_ReturnsFalse_WhenInvalid(string? input) {
        bool success = WebhookJobId.TryParse(input, out WebhookJobId id);
        Assert.False(success);
        Assert.Equal(default, id);
    }

    [Fact]
    public void TryParse_Span_ReturnsTrue_WhenValid() {
        bool success = WebhookJobId.TryParse("job_span".AsSpan(), out WebhookJobId id);
        Assert.True(success);
        Assert.Equal("job_span", id.Value);
    }

    [Fact]
    public void TryParse_Span_ReturnsFalse_WhenWhitespace() {
        bool success = WebhookJobId.TryParse("   ".AsSpan(), out WebhookJobId id);
        Assert.False(success);
        Assert.Equal(default, id);
    }

    [Fact]
    public void TryParse_Utf8_ReturnsTrue_WhenValid() {
        byte[] bytes = Encoding.UTF8.GetBytes("job_utf8");
        bool success = WebhookJobId.TryParse(bytes.AsSpan(), out WebhookJobId id);
        Assert.True(success);
        Assert.Equal("job_utf8", id.Value);
    }

    [Fact]
    public void TryParse_Utf8_ReturnsFalse_WhenEmpty() {
        bool success = WebhookJobId.TryParse(ReadOnlySpan<byte>.Empty, out WebhookJobId id);
        Assert.False(success);
        Assert.Equal(default, id);
    }

    [Fact]
    public void TryFormat_Span_FormatsCorrectly() {
        WebhookJobId id = new("job_format");
        Span<char> destination = stackalloc char[32];

        bool success = id.TryFormat(destination, out int charsWritten);

        Assert.True(success);
        Assert.Equal("job_format".Length, charsWritten);
        Assert.Equal("job_format", destination[..charsWritten].ToString());
    }

    [Fact]
    public void TryFormat_Span_ReturnsFalse_WhenBufferTooSmall() {
        WebhookJobId id = new("job_format_very_long_identifier");
        Span<char> destination = stackalloc char[5];

        bool success = id.TryFormat(destination, out int charsWritten);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormat_Utf8_FormatsCorrectly() {
        WebhookJobId id = new("job_format_utf8");
        Span<byte> destination = stackalloc byte[32];

        bool success = id.TryFormat(destination, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("job_format_utf8".Length, bytesWritten);
        Assert.Equal("job_format_utf8", Encoding.UTF8.GetString(destination[..bytesWritten]));
    }

    [Fact]
    public void TryFormat_Utf8_ReturnsFalse_WhenBufferTooSmall() {
        WebhookJobId id = new("job_format_utf8_long");
        Span<byte> destination = stackalloc byte[4];

        bool success = id.TryFormat(destination, out int bytesWritten);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void ComparisonAndEquality_WorksProperly() {
        WebhookJobId idA = new("job_a");
        WebhookJobId idB = new("job_b");
        WebhookJobId idA2 = new("job_a");

        Assert.Equal(idA, idA2);
        Assert.NotEqual(idA, idB);
        Assert.True(idA.CompareTo(idB) < 0);
        Assert.True(idB.CompareTo(idA) > 0);
        Assert.Equal(0, idA.CompareTo(idA2));

        IComparable nonGeneric = idA;
        Assert.Equal(1, nonGeneric.CompareTo(null));
        Assert.Throws<ArgumentException>(() => nonGeneric.CompareTo("not-a-job-id"));
    }

    [Fact]
    public void ExplicitInterfaces_FormatAndParseProperly() {
        WebhookJobId id = new("job_explicit");

        IFormattable formattable = id;
        Assert.Equal("job_explicit", formattable.ToString(null, CultureInfo.InvariantCulture));

        WebhookJobId parsed = ParseGeneric<WebhookJobId>("job_explicit");
        Assert.Equal(id, parsed);

        bool tryParsed = TryParseGeneric("job_explicit", out WebhookJobId res);
        Assert.True(tryParsed);
        Assert.Equal(id, res);

        WebhookJobId spanParsed = SpanParseGeneric<WebhookJobId>("job_explicit".AsSpan());
        Assert.Equal(id, spanParsed);

        byte[] utf8 = Encoding.UTF8.GetBytes("job_explicit");
        WebhookJobId utf8Parsed = Utf8SpanParseGeneric<WebhookJobId>(utf8);
        Assert.Equal(id, utf8Parsed);
    }

    private static T ParseGeneric<T>(string s) where T : IParsable<T> => T.Parse(s, CultureInfo.InvariantCulture);
    private static bool TryParseGeneric<T>(string s, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? result) where T : IParsable<T> => T.TryParse(s, CultureInfo.InvariantCulture, out result);
    private static T SpanParseGeneric<T>(ReadOnlySpan<char> s) where T : ISpanParsable<T> => T.Parse(s, CultureInfo.InvariantCulture);
    private static T Utf8SpanParseGeneric<T>(ReadOnlySpan<byte> s) where T : IUtf8SpanParsable<T> => T.Parse(s, CultureInfo.InvariantCulture);

    [Fact]
    public void JsonSerialization_WorksRoundtrip() {
        WebhookJobId original = new("job_json_123");
        string json = JsonSerializer.Serialize(original);

        Assert.Equal("\"job_json_123\"", json);

        WebhookJobId deserialized = JsonSerializer.Deserialize<WebhookJobId>(json);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void JsonDictionaryKey_WorksRoundtrip() {
        Dictionary<WebhookJobId, int> map = new() {
            [new WebhookJobId("job_k1")] = 100,
            [new WebhookJobId("job_k2")] = 200
        };

        string json = JsonSerializer.Serialize(map);
        Dictionary<WebhookJobId, int>? deserialized = JsonSerializer.Deserialize<Dictionary<WebhookJobId, int>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(100, deserialized[new WebhookJobId("job_k1")]);
        Assert.Equal(200, deserialized[new WebhookJobId("job_k2")]);
    }

    [Fact]
    public void DictionaryAlternateLookup_AllowsZeroAllocationSpanLookup() {
        Dictionary<WebhookJobId, string> dict = new(WebhookJobId.OrdinalComparer) {
            [new WebhookJobId("job_alpha")] = "alpha_val",
            [new WebhookJobId("job_beta")] = "beta_val"
        };

        Dictionary<WebhookJobId, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            dict.GetAlternateLookup<ReadOnlySpan<char>>();

        Assert.True(lookup.TryGetValue("job_alpha".AsSpan(), out string? val));
        Assert.Equal("alpha_val", val);

        Assert.False(lookup.TryGetValue("job_gamma".AsSpan(), out _));
    }

    [Fact]
    public void OrdinalIgnoreCaseComparer_MatchesCaseInsensitively() {
        Dictionary<WebhookJobId, string> dict = new(WebhookJobId.OrdinalIgnoreCaseComparer) {
            [new WebhookJobId("JOB_UPPER")] = "upper_val"
        };

        Dictionary<WebhookJobId, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            dict.GetAlternateLookup<ReadOnlySpan<char>>();

        Assert.True(lookup.TryGetValue("job_upper".AsSpan(), out string? val));
        Assert.Equal("upper_val", val);
    }
}
