using Microsoft.Extensions.Time.Testing;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;

/// <summary>
/// Tests for <see cref="GuidV7"/>.
/// Naming convention: {Area}_{Scenario}_{ExpectedResult}
/// </summary>
public sealed class GuidV7Tests {

    // =========================================================================
    // Generation
    // =========================================================================

    [Fact]
    public void Generation_NewId_ReturnsVersion7Guid() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(7, id.Value.Version);
    }

    [Fact]
    public void Generation_NewId_IsNotEmpty() {
        GuidV7 id = GuidV7.NewId();
        Assert.NotEqual(GuidV7.Empty, id);
        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Generation_TwoConsecutiveCalls_ProduceDifferentIds() {
        Assert.NotEqual(GuidV7.NewId(), GuidV7.NewId());
    }

    [Fact]
    public void Generation_NewId_WithTimeProvider_UsesProvidedTime() {
        DateTimeOffset fixedTime = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        GuidV7 id = GuidV7.NewId(new FakeTimeProvider(fixedTime));
        Assert.Equal(fixedTime.ToUnixTimeMilliseconds(), id.GetTimestamp().ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Generation_NewId_WithNullTimeProvider_ThrowsArgumentNullException() {
        // Preca throws a subclass of ArgumentNullException — ThrowsAny covers the hierarchy
        Assert.ThrowsAny<ArgumentNullException>(() => GuidV7.NewId(null!));
    }

    [Fact]
    public void Generation_NewId_WithUnixTimestamp_EmbedsCorrectTimestamp() {
        UnixTimestamp ts = UnixTimestamp.Now;
        GuidV7 id = GuidV7.NewId(ts);

        Assert.Equal(ts.TotalMilliseconds, id.GetTimestamp().ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Generation_NewId_WithUnixTimestamp_ReturnsVersion7Guid() {
        GuidV7 id = GuidV7.NewId(UnixTimestamp.Now);
        Assert.Equal(7, id.Value.Version);
    }

    [Fact]
    public void Generation_NewId_WithUnixTimestamp_LaterTimestampProducesGreaterId() {
        UnixTimestamp earlier = UnixTimestamp.Now;
        UnixTimestamp later = UnixTimestamp.FromMilliseconds(earlier.TotalMilliseconds + 1);

        GuidV7 a = GuidV7.NewId(earlier);
        GuidV7 b = GuidV7.NewId(later);

        Assert.True(a < b, "ID with earlier timestamp should be less than ID with later timestamp");
    }

    [Fact]
    public void Generation_NewId_WithUnixTimestamp_SameTimestampProducesUniqueIds() {
        UnixTimestamp ts = UnixTimestamp.Now;

        GuidV7 a = GuidV7.NewId(ts);
        GuidV7 b = GuidV7.NewId(ts);

        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("2020-01-01T00:00:00Z")]
    [InlineData("2025-06-15T12:34:56.789Z")]
    [InlineData("1970-01-01T00:00:00.001Z")]
    public void Generation_NewId_WithUnixTimestamp_RoundTripsVariousDates(string isoDate) {
        DateTimeOffset dto = DateTimeOffset.Parse(isoDate).ToUniversalTime();
        UnixTimestamp ts = UnixTimestamp.From(dto);

        GuidV7 id = GuidV7.NewId(ts);

        Assert.Equal(ts.TotalMilliseconds, id.GetTimestamp().ToUnixTimeMilliseconds());
    }


    [Fact]
    public void Generation_Empty_HasZeroGuid() {
        Assert.Equal(Guid.Empty, GuidV7.Empty.Value);
    }

    [Fact]
    public void Generation_DefaultStruct_EqualsEmpty() {
        Assert.Equal(GuidV7.Empty, default);
    }

    [Fact]
    public void Generation_IdsGeneratedAtDifferentTimes_AreMonotonicallyOrdered() {
        FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
        List<GuidV7> ids = [];
        for(int i = 0; i < 10; i++) {
            ids.Add(GuidV7.NewId(fake));
            fake.Advance(TimeSpan.FromMilliseconds(1));
        }
        for(int i = 1; i < ids.Count; i++) {
            Assert.True(ids[i].CompareTo(ids[i - 1]) > 0,
                $"ID[{i}] should be > ID[{i - 1}]");
        }
    }

    // =========================================================================
    // Timestamp Extraction
    // =========================================================================

    [Fact]
    public void Timestamp_GetTimestamp_IsApproximatelyNow() {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        GuidV7 id = GuidV7.NewId();
        DateTimeOffset after = DateTimeOffset.UtcNow;

        long extracted = id.GetTimestamp().ToUnixTimeMilliseconds();

        Assert.InRange(extracted,
            before.ToUnixTimeMilliseconds() - 1,
            after.ToUnixTimeMilliseconds() + 1);
    }

    [Fact]
    public void Timestamp_GetTimestamp_WithFakeTime_IsExact() {
        DateTimeOffset fixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        GuidV7 id = GuidV7.NewId(new FakeTimeProvider(fixedTime));
        Assert.Equal(fixedTime.ToUnixTimeMilliseconds(), id.GetTimestamp().ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Timestamp_UnixTimestamp_MatchesGetTimestamp() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(id.GetTimestamp().ToUnixTimeMilliseconds(), id.UnixTimestamp.TotalMilliseconds);
    }

    [Fact]
    public void Timestamp_GetTimestamp_IsUtc() {
        Assert.Equal(TimeSpan.Zero, GuidV7.NewId().GetTimestamp().Offset);
    }

    [Theory]
    [InlineData("2020-01-01T00:00:00Z")]
    [InlineData("2025-06-15T12:34:56.789Z")]
    [InlineData("1970-01-01T00:00:00.001Z")]
    public void Timestamp_GetTimestamp_RoundTripsVariousDates(string isoDate) {
        DateTimeOffset original = DateTimeOffset.Parse(isoDate).ToUniversalTime();
        GuidV7 id = GuidV7.NewId(new FakeTimeProvider(original));
        Assert.Equal(original.ToUnixTimeMilliseconds(), id.GetTimestamp().ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Timestamp_Empty_ReturnsEpoch() {
        Assert.Equal(0L, GuidV7.Empty.GetTimestamp().ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Timestamp_LaterIdHasGreaterOrEqualTimestamp() {
        FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
        GuidV7 a = GuidV7.NewId(fake);
        fake.Advance(TimeSpan.FromMilliseconds(1));
        GuidV7 b = GuidV7.NewId(fake);
        Assert.True(b.GetTimestamp() >= a.GetTimestamp());
    }

    // =========================================================================
    // Parsing
    // =========================================================================

    [Fact]
    public void Parse_ValidV7String_Succeeds() {
        string s = Guid.CreateVersion7().ToString("D");
        GuidV7 result = GuidV7.Parse(s);
        Assert.Equal(7, result.Value.Version);
        Assert.Equal(s, result.ToString());
    }

    [Fact]
    public void Parse_NullString_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => GuidV7.Parse(null!));
    }

    [Fact]
    public void Parse_V4Guid_ThrowsFormatException() {
        Assert.Throws<FormatException>(() => GuidV7.Parse(Guid.NewGuid().ToString("D")));
    }

    [Fact]
    public void Parse_InvalidString_ThrowsFormatException() {
        Assert.Throws<FormatException>(() => GuidV7.Parse("not-a-guid"));
    }

    [Fact]
    public void Parse_EmptyString_ThrowsFormatException() {
        Assert.Throws<FormatException>(() => GuidV7.Parse(string.Empty));
    }

    [Fact]
    public void Parse_Roundtrip_PreservesValue() {
        GuidV7 original = GuidV7.NewId();
        Assert.Equal(original, GuidV7.Parse(original.ToString()));
    }

    [Fact]
    public void TryParse_ValidV7String_ReturnsTrueAndResult() {
        string s = Guid.CreateVersion7().ToString("D");
        bool ok = GuidV7.TryParse(s, out GuidV7 result);
        Assert.True(ok);
        Assert.Equal(7, result.Value.Version);
    }

    [Fact]
    public void TryParse_NullString_ReturnsFalse() {
        bool ok = GuidV7.TryParse(null, out GuidV7 result);
        Assert.False(ok);
        Assert.Equal(GuidV7.Empty, result);
    }

    [Fact]
    public void TryParse_V4Guid_ReturnsFalse() {
        Assert.False(GuidV7.TryParse(Guid.NewGuid().ToString("D"), out _));
    }

    [Fact]
    public void TryParse_InvalidString_ReturnsFalse() {
        Assert.False(GuidV7.TryParse("garbage", out _));
    }

    [Theory]
    [InlineData("D")]
    [InlineData("N")]
    [InlineData("B")]
    [InlineData("P")]
    public void TryParse_AcceptsAllStandardGuidFormats(string format) {
        string s = Guid.CreateVersion7().ToString(format);
        bool ok = GuidV7.TryParse(s, out GuidV7 result);
        Assert.True(ok, $"format '{format}' should be accepted");
        Assert.Equal(7, result.Value.Version);
    }

    [Fact]
    public void TryParse_Span_ValidV7_Succeeds() {
        string s = Guid.CreateVersion7().ToString("D");
        Assert.True(GuidV7.TryParse(s.AsSpan(), out _));
    }

    [Fact]
    public void ExplicitCast_FromV7Guid_Succeeds() {
        Guid v7 = Guid.CreateVersion7();
        GuidV7 result = (GuidV7)v7;
        Assert.Equal(v7, result.Value);
    }

    [Fact]
    public void ExplicitCast_FromV4Guid_ThrowsInvalidCastException() {
        Assert.Throws<InvalidCastException>(() => { GuidV7 _ = (GuidV7)Guid.NewGuid(); });
    }

    [Fact]
    public void ExplicitCast_FromEmptyGuid_ThrowsInvalidCastException() {
        Assert.Throws<InvalidCastException>(() => { GuidV7 _ = (GuidV7)Guid.Empty; });
    }

    // =========================================================================
    // Formatting
    // =========================================================================

    [Fact]
    public void Format_ToString_IsHyphenatedFormat() {
        string s = GuidV7.NewId().ToString();
        Assert.Equal(36, s.Length);
        Assert.Equal('-', s[8]);
        Assert.Equal('-', s[13]);
        Assert.Equal('-', s[18]);
        Assert.Equal('-', s[23]);
    }

    [Theory]
    [InlineData("D", 36)]
    [InlineData("N", 32)]
    [InlineData("B", 38)]
    [InlineData("P", 38)]
    public void Format_ToString_WithFormat_ProducesCorrectLength(string format, int expectedLength) {
        Assert.Equal(expectedLength, GuidV7.NewId().ToString(format).Length);
    }

    [Fact]
    public void Format_ToString_NullFormat_FallsBackToD() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(id.ToString("D"), id.ToString(null));
    }

    [Fact]
    public void Format_ISpanFormattable_TryFormat_Succeeds() {
        GuidV7 id = GuidV7.NewId();
        Span<char> buf = stackalloc char[36];
        bool ok = ((ISpanFormattable)id).TryFormat(buf, out int written, "D", null);
        Assert.True(ok);
        Assert.Equal(36, written);
        Assert.Equal(id.ToString(), new string(buf));
    }

    [Fact]
    public void Format_ISpanFormattable_TooSmallBuffer_ReturnsFalse() {
        Span<char> buf = stackalloc char[10];
        bool ok = ((ISpanFormattable)GuidV7.NewId()).TryFormat(buf, out int written, "D", null);
        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void Format_ISpanFormattable_EmptyFormat_DefaultsToD() {
        GuidV7 id = GuidV7.NewId();
        Span<char> buf = stackalloc char[36];
        ((ISpanFormattable)id).TryFormat(buf, out int written, ReadOnlySpan<char>.Empty, null);
        Assert.Equal(id.ToString("D"), new string(buf[..written]));
    }

    [Fact]
    public void Format_IUtf8SpanFormattable_TryFormat_Succeeds() {
        GuidV7 id = GuidV7.NewId();
        Span<byte> buf = stackalloc byte[36];
        bool ok = ((IUtf8SpanFormattable)id).TryFormat(buf, out int written, "D", null);
        Assert.True(ok);
        Assert.Equal(36, written);
        Assert.Equal(id.ToString(), System.Text.Encoding.UTF8.GetString(buf[..written]));
    }

    [Fact]
    public void Format_IUtf8SpanFormattable_TooSmallBuffer_ReturnsFalse() {
        Span<byte> buf = stackalloc byte[5];
        bool ok = ((IUtf8SpanFormattable)GuidV7.NewId()).TryFormat(buf, out int written, "D", null);
        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void Format_IFormattable_MatchesPublicToString() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(id.ToString("D"), ((IFormattable)id).ToString("D", null));
    }

    // =========================================================================
    // Conversion
    // =========================================================================

    [Fact]
    public void Conversion_ToGuid_ReturnsUnderlyingGuid() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(id.Value, id.ToGuid());
    }

    [Fact]
    public void Conversion_ImplicitCastToGuid_Works() {
        GuidV7 id = GuidV7.NewId();
        Guid g = id;
        Assert.Equal(id.Value, g);
    }

    [Fact]
    public void Conversion_ToHexString_Is32Chars() {
        Assert.Equal(32, GuidV7.NewId().ToHexString().Value.Length);
    }

    [Fact]
    public void Conversion_ToHexString_RoundTrips() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(id.Value, new Guid(id.ToHexString().ToBytes()));
    }

    [Fact]
    public void Conversion_ToBase64Url_Is22Chars() {
        Assert.Equal(22, GuidV7.NewId().ToBase64Url().Value.Length);
    }

    [Fact]
    public void Conversion_ToBase64Url_RoundTrips() {
        GuidV7 id = GuidV7.NewId();
        Span<byte> decoded = stackalloc byte[16];
        id.ToBase64Url().TryDecode(decoded, out _);
        Assert.Equal(id.Value, new Guid(decoded));
    }

    [Fact]
    public void Conversion_ToHexString_And_ToBase64Url_RepresentSameBytes() {
        GuidV7 id = GuidV7.NewId();
        byte[] fromHex = id.ToHexString().ToBytes();
        Span<byte> fromB64 = stackalloc byte[16];
        id.ToBase64Url().TryDecode(fromB64, out _);
        Assert.Equal(fromHex, fromB64.ToArray());
    }

    // =========================================================================
    // Equality & Comparison
    // =========================================================================

    [Fact]
    public void Equality_SameId_IsEqual() {
        GuidV7 id = GuidV7.NewId();
        GuidV7 copy = GuidV7.Parse(id.ToString());
        Assert.Equal(id, copy);
        Assert.True(id == copy);
    }

    [Fact]
    public void Equality_DifferentIds_AreNotEqual() {
        GuidV7 a = GuidV7.NewId();
        GuidV7 b = GuidV7.NewId();
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_GetHashCode_SameIdSameHash() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(id.GetHashCode(), GuidV7.Parse(id.ToString()).GetHashCode());
    }

    [Fact]
    public void Comparison_CompareTo_LaterIdIsGreater() {
        FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
        GuidV7 a = GuidV7.NewId(fake);
        fake.Advance(TimeSpan.FromMilliseconds(1));
        GuidV7 b = GuidV7.NewId(fake);
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
    }

    [Fact]
    public void Comparison_CompareTo_SameIdReturnsZero() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal(0, id.CompareTo(id));
    }

    [Fact]
    public void Comparison_Operators_LessThanGreaterThan() {
        FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
        GuidV7 a = GuidV7.NewId(fake);
        fake.Advance(TimeSpan.FromMilliseconds(1));
        GuidV7 b = GuidV7.NewId(fake);
        Assert.True(a < b);
        Assert.True(b > a);
        Assert.False(a > b);
        Assert.False(b < a);
    }

    [Fact]
    public void Comparison_Operators_GreaterOrEqualLessOrEqual_SameId() {
        GuidV7 id = GuidV7.NewId();
        Assert.True(id >= id);
        Assert.True(id <= id);
    }

    [Fact]
    public void Comparison_OrderBy_SortsChronologically() {
        FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
        List<GuidV7> ids = Enumerable.Range(0, 10).Select(_ => {
            GuidV7 id = GuidV7.NewId(fake);
            fake.Advance(TimeSpan.FromMilliseconds(1));
            return id;
        }).ToList();

        List<GuidV7> sorted = ids.OrderBy(x => x).ToList();
        Assert.Equal(ids, sorted);
    }

    [Fact]
    public void Equality_CanBeUsedAsDictionaryKey() {
        Dictionary<GuidV7, string> dict = [];
        GuidV7 id = GuidV7.NewId();
        dict[id] = "value";
        Assert.Equal("value", dict[id]);
    }

    [Fact]
    public void Equality_HashSet_NoDuplicates() {
        HashSet<GuidV7> set = [];
        GuidV7 id = GuidV7.NewId();
        Assert.True(set.Add(id));
        Assert.False(set.Add(id));
        Assert.Single(set);
    }

    // =========================================================================
    // JSON
    // =========================================================================

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    [Fact]
    public void Json_Serialize_ProducesQuotedHyphenatedString() {
        GuidV7 id = GuidV7.NewId();
        Assert.Equal($"\"{id}\"", JsonSerializer.Serialize(id, _jsonOptions));
    }

    [Fact]
    public void Json_Deserialize_ValidV7String_Succeeds() {
        GuidV7 original = GuidV7.NewId();
        GuidV7 result = JsonSerializer.Deserialize<GuidV7>($"\"{original}\"", _jsonOptions);
        Assert.Equal(original, result);
    }

    [Fact]
    public void Json_Deserialize_V4String_ThrowsJsonException() {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<GuidV7>($"\"{Guid.NewGuid()}\"", _jsonOptions));
    }

    [Fact]
    public void Json_Deserialize_InvalidString_ThrowsJsonException() {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<GuidV7>("\"not-a-guid\"", _jsonOptions));
    }

    [Fact]
    public void Json_Deserialize_NonStringToken_ThrowsJsonException() {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<GuidV7>("42", _jsonOptions));
    }

    [Fact]
    public void Json_SerializeDeserialize_Roundtrip() {
        GuidV7 original = GuidV7.NewId();
        string json = JsonSerializer.Serialize(original, _jsonOptions);
        Assert.Equal(original, JsonSerializer.Deserialize<GuidV7>(json, _jsonOptions));
    }

    [Fact]
    public void Json_Serialize_InObject_ContainsCorrectValue() {
        GuidV7 id = GuidV7.NewId();
        string json = JsonSerializer.Serialize(new { Id = id, Name = "test" }, _jsonOptions);
        Assert.Contains(id.ToString(), json);
    }

    // =========================================================================
    // Interface Contracts
    // =========================================================================

    [Fact]
    public void Interface_IParsable_Parse_Succeeds() {
        GuidV7 original = GuidV7.NewId();
        Assert.Equal(original, ParseVia<GuidV7>(original.ToString()));
    }

    [Fact]
    public void Interface_IParsable_TryParse_Succeeds() {
        GuidV7 original = GuidV7.NewId();
        Assert.True(TryParseVia<GuidV7>(original.ToString(), out GuidV7 result));
        Assert.Equal(original, result);
    }

    [Fact]
    public void Interface_ISpanParsable_TryParse_Succeeds() {
        GuidV7 original = GuidV7.NewId();
        Assert.True(TryParseSpanVia<GuidV7>(original.ToString().AsSpan(), out GuidV7 result));
        Assert.Equal(original, result);
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Fact]
    public void EdgeCase_Empty_TimestampIsEpoch() {
        Assert.Equal(0L, GuidV7.Empty.GetTimestamp().ToUnixTimeMilliseconds());
    }

    [Fact]
    public void EdgeCase_Empty_UnixTimestampIsZero() {
        Assert.Equal(0L, GuidV7.Empty.UnixTimestamp.TotalMilliseconds);
    }

    [Fact]
    public void EdgeCase_AllGeneratedIds_AreVersion7() {
        for(int i = 0; i < 20; i++)
            Assert.Equal(7, GuidV7.NewId().Value.Version);
    }

    [Fact]
    public void EdgeCase_LargeNumberOfIds_AllUnique() {
        const int count = 10_000;
        HashSet<GuidV7> ids = new(count);
        for(int i = 0; i < count; i++)
            Assert.True(ids.Add(GuidV7.NewId()), $"ID {i} was a duplicate");
        Assert.Equal(count, ids.Count);
    }

    [Fact]
    public void EdgeCase_FakeTimeProvider_Advance_ProducesOrderedIds() {
        FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
        GuidV7 a = GuidV7.NewId(fake);
        fake.Advance(TimeSpan.FromMilliseconds(1));
        GuidV7 b = GuidV7.NewId(fake);
        fake.Advance(TimeSpan.FromMilliseconds(1));
        GuidV7 c = GuidV7.NewId(fake);

        Assert.True(a < b, "a should be less than b");
        Assert.True(b < c, "b should be less than c");
    }

    // =========================================================================
    // Generic helpers — required because static interface members can only be
    // called via a type parameter, not directly via the interface type.
    // =========================================================================

    private static T ParseVia<T>(string s) where T : IParsable<T> {
        return T.Parse(s, null);
    }

    private static bool TryParseVia<T>(string s, out T result) where T : IParsable<T> {
        return T.TryParse(s, null, out result);
    }

    private static bool TryParseSpanVia<T>(ReadOnlySpan<char> s, out T result) where T : ISpanParsable<T> {
        return T.TryParse(s, null, out result);
    }
}