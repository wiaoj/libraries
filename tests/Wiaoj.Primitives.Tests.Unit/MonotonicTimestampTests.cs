using System.Text;
using System.Text.Json;
using Wiaoj.Primitives;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Primitives;

[Trait("Category", "Unit")]
[Trait("Feature", "Primitives")]
[Trait("Component", "MonotonicTimestamp")]
public sealed class MonotonicTimestampTests {

    public sealed class TheArithmeticAndOperators {
        [Fact]
        public void Add_TimeSpan_AdvancesTimestampCorrectly() {
            MonotonicTimestamp start = MonotonicTimestamp.FromRawTicks(10_000);
            TimeSpan duration = TimeSpan.FromSeconds(2);

            MonotonicTimestamp result = start + duration;

            Assert.True(result > start);
            TimeSpan elapsed = result - start;
            Assert.Equal(duration.TotalMilliseconds, elapsed.TotalMilliseconds, precision: 1);
        }

        [Fact]
        public void Subtract_TimeSpan_DecreasesTimestampCorrectly() {
            MonotonicTimestamp start = MonotonicTimestamp.FromRawTicks(1_000_000);
            TimeSpan duration = TimeSpan.FromSeconds(1);

            MonotonicTimestamp result = start - duration;

            Assert.True(result < start);
            TimeSpan diff = start - result;
            Assert.Equal(duration.TotalMilliseconds, diff.TotalMilliseconds, precision: 1);
        }

        [Fact]
        public void Comparisons_EvaluateAccurately() {
            MonotonicTimestamp smaller = MonotonicTimestamp.FromRawTicks(100);
            MonotonicTimestamp larger = MonotonicTimestamp.FromRawTicks(200);

            Assert.True(smaller < larger);
            Assert.True(larger > smaller);
            Assert.True(smaller <= larger);
            Assert.True(larger >= smaller);
            Assert.False(smaller == larger);
            Assert.True(smaller != larger);
        }

        [Fact]
        public void EmptyAndZero_AreEquivalentToDefault() {
            Assert.True(MonotonicTimestamp.Empty.IsEmpty);
            Assert.True(MonotonicTimestamp.Zero.IsEmpty);
            Assert.True(default(MonotonicTimestamp).IsEmpty);
            Assert.Equal(MonotonicTimestamp.Empty, MonotonicTimestamp.Zero);
            Assert.Equal(0, MonotonicTimestamp.Empty.RawTicks);
        }
    }

    public sealed class TheParsingAndFormatting {
        [Fact]
        public void TryParse_ValidStringAndSpans_ParsesCorrectly() {
            const long rawTicks = 123456789;
            string tickStr = rawTicks.ToString();

            Assert.True(MonotonicTimestamp.TryParse(tickStr, out MonotonicTimestamp fromString));
            Assert.True(MonotonicTimestamp.TryParse(tickStr.AsSpan(), out MonotonicTimestamp fromCharSpan));
            Assert.True(MonotonicTimestamp.TryParse(Encoding.UTF8.GetBytes(tickStr), out MonotonicTimestamp fromUtf8Span));

            Assert.Equal(rawTicks, fromString.RawTicks);
            Assert.Equal(rawTicks, fromCharSpan.RawTicks);
            Assert.Equal(rawTicks, fromUtf8Span.RawTicks);
        }

        [Fact]
        public void TryFormat_CharAndUtf8Span_FormatsCorrectly() {
            MonotonicTimestamp ts = MonotonicTimestamp.FromRawTicks(987654321);

            Span<char> charBuffer = stackalloc char[32];
            Assert.True(ts.TryFormat(charBuffer, out int charsWritten));
            Assert.Equal("987654321", charBuffer[..charsWritten].ToString());

            Span<byte> utf8Buffer = stackalloc byte[32];
            Assert.True(ts.TryFormat(utf8Buffer, out int bytesWritten));
            Assert.Equal("987654321", Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]));
        }
    }

    public sealed class TheAlternateLookupSupport {
        [Fact]
        public void DictionaryLookup_UsingCharSpan_FindsItemWithoutAllocation() {
            Dictionary<MonotonicTimestamp, string> map = new(MonotonicTimestamp.OrdinalComparer);
            MonotonicTimestamp key = MonotonicTimestamp.FromRawTicks(55555);
            map[key] = "found_by_char_span";

            var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();

            Assert.True(lookup.TryGetValue("55555".AsSpan(), out string? value));
            Assert.Equal("found_by_char_span", value);
        }

        [Fact]
        public void DictionaryLookup_UsingUtf8ByteSpan_FindsItemWithoutAllocation() {
            Dictionary<MonotonicTimestamp, string> map = new(MonotonicTimestamp.OrdinalComparer);
            MonotonicTimestamp key = MonotonicTimestamp.FromRawTicks(88888);
            map[key] = "found_by_utf8_span";

            var lookup = map.GetAlternateLookup<ReadOnlySpan<byte>>();

            byte[] utf8Key = "88888"u8.ToArray();
            Assert.True(lookup.TryGetValue(utf8Key.AsSpan(), out string? value));
            Assert.Equal("found_by_utf8_span", value);
        }
    }

    public sealed class TheJsonSerialization {
        [Fact]
        public void Json_SerializeAndDeserialize_PreservesRawTicks() {
            MonotonicTimestamp original = MonotonicTimestamp.FromRawTicks(1234567890123);

            string json = JsonSerializer.Serialize(original);
            Assert.Equal("1234567890123", json);

            MonotonicTimestamp deserialized = JsonSerializer.Deserialize<MonotonicTimestamp>(json);
            Assert.Equal(original, deserialized);
        }

        [Fact]
        public void Json_DeserializeFromString_SupportsCompatibility() {
            string jsonString = "\"1234567890123\"";
            MonotonicTimestamp deserialized = JsonSerializer.Deserialize<MonotonicTimestamp>(jsonString);

            Assert.Equal(1234567890123, deserialized.RawTicks);
        }

        [Fact]
        public void Json_DictionaryKey_SerializesAndDeserializesSuccessfully() {
            Dictionary<MonotonicTimestamp, string> dict = new() {
                [MonotonicTimestamp.FromRawTicks(42)] = "answer"
            };

            string json = JsonSerializer.Serialize(dict);
            Assert.Contains("\"42\":\"answer\"", json);

            Dictionary<MonotonicTimestamp, string>? deserialized = JsonSerializer.Deserialize<Dictionary<MonotonicTimestamp, string>>(json);
            Assert.NotNull(deserialized);
            Assert.Equal("answer", deserialized[MonotonicTimestamp.FromRawTicks(42)]);
        }
    }
}