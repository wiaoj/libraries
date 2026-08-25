using System.Text;
using Wiaoj.DistributedCounter;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "CounterKey")]
public sealed class CounterKeyTests {

    public sealed class TheParseMethod {

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("\r\n")]
        public void GivenNullOrWhitespaceString_ThrowsArgumentException(string? invalidKey) {
            // Arrange & Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => CounterKey.Parse(invalidKey!));
        }

        [Theory]
        [InlineData("users:login", "users:login")]
        [InlineData("  users:login  ", "users:login")]
        [InlineData("\tusers:login\r\n", "users:login")]
        public void GivenValidStringWithWhitespace_TrimsCorrectly(string raw, string expected) {
            // Arrange & Act
            CounterKey key = CounterKey.Parse(raw);

            // Assert
            Assert.Equal(expected, key.Value);
            Assert.False(key.IsEmpty);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\r\n")]
        public void GivenEmptyOrWhitespaceSpan_ThrowsArgumentException(string invalid) {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => CounterKey.Parse(invalid.AsSpan()));
        }

        [Fact]
        public void GivenValidSpanWithWhitespace_TrimsWithoutRedundantAllocation() {
            // Arrange
            string raw = "  my_custom_key  ";

            // Act
            CounterKey key = CounterKey.Parse(raw.AsSpan());

            // Assert
            Assert.Equal("my_custom_key", key.Value);
        }

        [Theory]
        [MemberData(nameof(Utf8TestCases))]
        public void GivenUtf8Bytes_ParsesOrThrowsCorrectly(byte[] bytes, bool shouldSucceed, string? expectedValue) {
            // Arrange & Act & Assert
            if(shouldSucceed) {
                CounterKey key = CounterKey.Parse(bytes.AsSpan());
                Assert.Equal(expectedValue, key.Value);
            }
            else {
                Assert.ThrowsAny<ArgumentException>(() => CounterKey.Parse(bytes.AsSpan()));
            }
        }

        public static TheoryData<byte[], bool, string?> Utf8TestCases => new() {
            { Encoding.UTF8.GetBytes("simple:key"), true, "simple:key" },
            { Encoding.UTF8.GetBytes("  trimmed:key  "), true, "trimmed:key" },
            { Encoding.UTF8.GetBytes(""), false, null },
            { Encoding.UTF8.GetBytes("   "), false, null },
            { [0x20, 0x09, 0x0A, 0x0D], false, null } // Space, Tab, LF, CR
        };
    }

    public sealed class TheTryParseMethod {

        [Theory]
        [InlineData("valid_key", true, "valid_key")]
        [InlineData("   ", false, "")]
        [InlineData(null, false, "")]
        public void GivenString_ReturnsExpectedSuccessAndValue(string? input, bool expectedSuccess, string expectedVal) {
            // Arrange & Act
            bool success = CounterKey.TryParse(input, out CounterKey result);

            // Assert
            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedVal, result.Value);
            Assert.Equal(!expectedSuccess, result.IsEmpty);
        }

        [Theory]
        [InlineData("span_valid", true, "span_valid")]
        [InlineData("   ", false, "")]
        [InlineData("", false, "")]
        public void GivenSpanChar_ReturnsExpectedSuccessAndValue(string input, bool expectedSuccess, string expectedVal) {
            // Arrange & Act
            bool success = CounterKey.TryParse(input.AsSpan(), out CounterKey result);

            // Assert
            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedVal, result.Value);
        }
    }

    public sealed class TheEqualityAndConversionRules {

        [Fact]
        public void DefaultStruct_And_Empty_AreEquivalentAndEmpty() {
            // Arrange
            CounterKey defaultKey = default;
            CounterKey emptyKey = CounterKey.Empty;

            // Assert
            Assert.True(defaultKey.IsEmpty);
            Assert.True(emptyKey.IsEmpty);
            Assert.Equal(string.Empty, defaultKey.Value);
            Assert.Equal(string.Empty, emptyKey.Value);
            Assert.Equal(defaultKey, emptyKey);
        }

        [Fact]
        public void ImplicitStringConversions_WorkBidirectionally() {
            // Arrange & Act
            CounterKey key = "test:conversion";
            string str = key;

            // Assert
            Assert.Equal("test:conversion", key.Value);
            Assert.Equal("test:conversion", str);
        }
    }

    public sealed class TheFormattingOperations {

        [Fact]
        public void TryFormat_CharSpan_WritesCorrectCharacters() {
            // Arrange
            CounterKey key = new("test:format:key");
            Span<char> destination = stackalloc char[32];

            // Act
            bool success = key.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(key.Value.Length, charsWritten);
            Assert.Equal(key.Value, destination[..charsWritten].ToString());
        }

        [Fact]
        public void TryFormat_Utf8Span_WritesCorrectBytes() {
            // Arrange
            CounterKey key = new("utf8:format");
            Span<byte> destination = stackalloc byte[32];

            // Act
            bool success = key.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(Encoding.UTF8.GetBytes("utf8:format"), destination[..bytesWritten].ToArray());
        }
    }

    public sealed class TheJsonSerialization {

        [Fact]
        public void SerializationAndDeserialization_WorksAsDirectString() {
            // Arrange
            CounterKey originalKey = new("orders:daily");

            // Act
            string json = System.Text.Json.JsonSerializer.Serialize(originalKey);
            CounterKey deserializedKey = System.Text.Json.JsonSerializer.Deserialize<CounterKey>(json);

            // Assert
            Assert.Equal("\"orders:daily\"", json);
            Assert.Equal(originalKey, deserializedKey);
        }

        [Fact]
        public void AsDictionaryKey_SerializesAndDeserializesCorrectly() {
            // Arrange
            Dictionary<CounterKey, int> dict = new() {
                { new CounterKey("key1"), 100 }
            };

            // Act
            string json = System.Text.Json.JsonSerializer.Serialize(dict);
            Dictionary<CounterKey, int>? deserialized = System.Text.Json.JsonSerializer.Deserialize<Dictionary<CounterKey, int>>(json);

            // Assert
            Assert.Contains("\"key1\":100", json);
            Assert.NotNull(deserialized);
            Assert.True(deserialized.ContainsKey(new CounterKey("key1")));
        }
    }

    public sealed class TheComparisonAndSorting {

        [Fact]
        public void CompareTo_SortsOrdinally() {
            // Arrange
            CounterKey keyA = new("a:key");
            CounterKey keyB = new("b:key");

            // Act & Assert
            Assert.True(keyA.CompareTo(keyB) < 0);
            Assert.True(keyB.CompareTo(keyA) > 0);
            Assert.Equal(0, keyA.CompareTo(new CounterKey("a:key")));
        }
    }
}