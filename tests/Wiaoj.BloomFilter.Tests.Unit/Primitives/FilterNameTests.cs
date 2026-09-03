using System.Text.Json;

namespace Wiaoj.BloomFilter.Tests.Unit.Primitives;

public sealed class FilterNameTests {
    public sealed class ParseMethod {
        [Theory]
        [InlineData("valid-name")]
        [InlineData("user_blacklist")]
        [InlineData("cache.v1")]
        [InlineData("A1-B2_C3.D4")]
        public void Should_ReturnValidFilterName_When_InputIsValid(string input) {
            // Arrange & Act
            FilterName filterName = FilterName.Parse(input);

            // Assert
            Assert.Equal(input, filterName.Value);
            Assert.Equal(input, filterName.ToString());
        }

        [Fact]
        public void Should_ThrowArgumentNullException_When_InputIsNull() {
            // Arrange
            string nullInput = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => FilterName.Parse(nullInput));
        }

        [Fact]
        public void Should_ThrowArgumentException_When_InputIsEmpty() {
            // Arrange
            string emptyInput = string.Empty;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => FilterName.Parse(emptyInput));
        }

        [Fact]
        public void Should_ThrowArgumentException_When_InputExceedsMaximumLength() {
            // Arrange
            string overlyLongInput = new('a', 129);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => FilterName.Parse(overlyLongInput));
        }

        [Theory]
        [InlineData("name with spaces")]
        [InlineData("name@domain")]
        [InlineData("filter#1")]
        [InlineData("name/slash")]
        public void Should_ThrowFormatException_When_InputContainsDisallowedCharacters(string input) {
            // Arrange & Act & Assert
            Assert.Throws<FormatException>(() => FilterName.Parse(input));
        }
    }

    public sealed class TryParseMethod {
        [Fact]
        public void Should_ReturnTrueAndSetResult_When_ValidSpanProvided() {
            // Arrange
            ReadOnlySpan<char> input = "valid-filter".AsSpan();

            // Act
            bool success = FilterName.TryParse(input, out FilterName result);

            // Assert
            Assert.True(success);
            Assert.Equal("valid-filter", result.Value);
        }

        [Fact]
        public void Should_ReturnFalseAndSetDefault_When_InvalidSpanProvided() {
            // Arrange
            ReadOnlySpan<char> input = "invalid filter name!".AsSpan();

            // Act
            bool success = FilterName.TryParse(input, out FilterName result);

            // Assert
            Assert.False(success);
            Assert.Equal(string.Empty, result.Value);
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_SerializeAndDeserializeCorrectly() {
            // Arrange
            FilterName original = FilterName.Parse("analytics-cache");

            // Act
            string json = JsonSerializer.Serialize(original);
            FilterName deserialized = JsonSerializer.Deserialize<FilterName>(json);

            // Assert
            Assert.Equal("\"analytics-cache\"", json);
            Assert.Equal(original, deserialized);
        }
    }
}