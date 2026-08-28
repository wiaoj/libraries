using System.Text;
using System.Text.Json;
using Wiaoj.Primitives;
using Xunit;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "KeysetPagination")]
public sealed class CursorTokenTests {

    public sealed class FromBytesMethod {
        [Fact]
        public void Should_Encode_Raw_Bytes() {
            // Arrange
            byte[] rawBytes = [1, 2, 3, 4, 5];

            // Act
            CursorToken token = CursorToken.FromBytes(rawBytes);

            // Assert
            Assert.False(token.IsEmpty);
            Span<byte> destination = stackalloc byte[16];
            bool decoded = token.TryDecode(destination, out int written);

            Assert.True(decoded);
            Assert.Equal(rawBytes.Length, written);
            Assert.True(rawBytes.AsSpan().SequenceEqual(destination[..written]));
        }

        [Fact]
        public void Should_Return_Empty_When_Input_Span_Is_Empty() {
            // Arrange & Act
            var token = CursorToken.FromBytes(ReadOnlySpan<byte>.Empty);

            // Assert
            Assert.True(token.IsEmpty);
            Assert.Equal(CursorToken.Empty, token);
        }
    }

    public sealed class FromUtf8Method {
        [Fact]
        public void Should_Encode_String_Input() {
            // Arrange
            Base64UrlString expectedBase64Url = Base64UrlString.FromUtf8("cursor_payload_100");

            // Act
            CursorToken token = CursorToken.FromUtf8("cursor_payload_100");

            // Assert
            Assert.Equal(expectedBase64Url.Value, token.Value);
            Assert.False(token.IsEmpty);
        }

        [Fact]
        public void Should_Encode_Utf8_Byte_Span_Input() {
            // Arrange
            byte[] utf8Bytes = "entity_id_8888"u8.ToArray();

            // Act
            var token = CursorToken.FromUtf8(utf8Bytes);

            // Assert
            Assert.False(token.IsEmpty);
            Assert.Equal(Base64UrlString.FromUtf8(utf8Bytes).Value, token.Value);
        }

        [Fact]
        public void Should_Return_Empty_When_String_Or_Span_Is_Empty() {
            // Arrange & Act
            var tokenFromString = CursorToken.FromUtf8(string.Empty);
            var tokenFromSpan = CursorToken.FromUtf8(ReadOnlySpan<byte>.Empty);

            // Assert
            Assert.True(tokenFromString.IsEmpty);
            Assert.True(tokenFromSpan.IsEmpty);
        }
    }

    public sealed class ParseMethod {
        [Fact]
        public void Should_Parse_Valid_Base64Url_String() {
            // Arrange
            string validPayload = Base64UrlString.FromUtf8("test_token_123").Value;

            // Act
            var token = CursorToken.Parse(validPayload);

            // Assert
            Assert.Equal(validPayload, token.Value);
        }

        [Fact]
        public void Should_Fail_To_Parse_Invalid_Base64Url() {
            // Arrange
            string invalidString = "invalid!@#$characters";

            // Act & Assert
            Assert.Throws<FormatException>(() => CursorToken.Parse(invalidString));
            Assert.False(CursorToken.TryParse(invalidString, out _));
        }
    }

    public sealed class TryDecodeMethod {
        [Fact]
        public void Should_Decode_Into_Destination_Span() {
            // Arrange
            byte[] rawBytes = [10, 20, 30, 40];
            var token = CursorToken.FromBytes(rawBytes);
            Span<byte> destination = stackalloc byte[16];

            // Act
            bool success = token.TryDecode(destination, out int bytesWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(rawBytes.Length, bytesWritten);
            Assert.True(rawBytes.AsSpan().SequenceEqual(destination[..bytesWritten]));
        }

        [Fact]
        public void Should_Return_False_When_Destination_Buffer_Is_Too_Small() {
            // Arrange
            byte[] rawBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
            var token = CursorToken.FromBytes(rawBytes);
            Span<byte> smallBuffer = stackalloc byte[3];

            // Act
            bool success = token.TryDecode(smallBuffer, out int bytesWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, bytesWritten);
        }

        [Fact]
        public void Should_Decode_Empty_Token_As_Zero_Bytes_Without_Failing() {
            // Arrange
            CursorToken token = CursorToken.Empty;
            Span<byte> destination = stackalloc byte[16];

            // Act
            bool success = token.TryDecode(destination, out int bytesWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(0, bytesWritten);
        }
    }

    public sealed class TryFormatMethod {
        [Fact]
        public void Should_Format_To_Char_Span() {
            // Arrange
            var token = CursorToken.FromUtf8("test_token_123");
            Span<char> destination = stackalloc char[token.Length];

            // Act
            bool success = token.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(token.Length, charsWritten);
            Assert.True(destination.SequenceEqual(token.Value.AsSpan()));
        }

        [Fact]
        public void Should_Format_To_Utf8_Span() {
            // Arrange
            var token = CursorToken.FromUtf8("test_token_123");
            Span<byte> destination = stackalloc byte[token.Length];

            // Act
            bool success = token.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(token.Length, bytesWritten);
        }

        [Fact]
        public void Should_Return_False_When_Destination_Buffer_Is_Too_Small() {
            // Arrange
            var token = CursorToken.FromUtf8("very_long_cursor_token_value_12345");
            Span<char> smallBuffer = stackalloc char[5];

            // Act
            bool success = token.TryFormat(smallBuffer, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }

        [Fact]
        public void Should_Return_False_When_Utf8_Destination_Buffer_Is_Too_Small() {
            // Arrange
            var token = CursorToken.FromUtf8("very_long_cursor_token_value_12345");
            Span<byte> smallBuffer = stackalloc byte[5];

            // Act
            bool success = token.TryFormat(smallBuffer, out int bytesWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, bytesWritten);
        }
    }

    public sealed class CompareToMethod {
        [Fact]
        public void Should_Order_Tokens_Ordinally() {
            // Arrange
            CursorToken tokenA = CursorToken.FromUtf8("aaa");
            CursorToken tokenB = CursorToken.FromUtf8("bbb");

            // Act & Assert
            Assert.True(tokenA < tokenB);
            Assert.True(tokenA <= tokenB);
            Assert.False(tokenA > tokenB);
            Assert.False(tokenA >= tokenB);
        }

        [Fact]
        public void Should_Comply_With_IComparable_Standard_Contract() {
            // Arrange
            var token = CursorToken.FromUtf8("cursor_abc");
            IComparable comparable = token;

            // Act & Assert
            Assert.Equal(1, comparable.CompareTo(null));
            Assert.Throws<ArgumentException>(() => comparable.CompareTo(12345));
        }

        [Fact]
        public void Should_Return_Zero_When_Compared_To_Itself() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("same_value");

            // Act & Assert
            Assert.Equal(0, token.CompareTo(token));
            Assert.True(token <= token);
            Assert.True(token >= token);
        }
    }

    public sealed class AlternateLookup {
        [Fact]
        public void Should_Lookup_Using_Char_Span() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("entity_id_9999");
            Dictionary<CursorToken, string> cache = new(CursorToken.OrdinalComparer) {
                [token] = "CachedPayload"
            };

            var lookup = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            // Act
            bool found = lookup.TryGetValue(token.Value.AsSpan(), out string? value);

            // Assert
            Assert.True(found);
            Assert.Equal("CachedPayload", value);
        }

        [Fact]
        public void Should_Lookup_Using_Utf8_Byte_Span() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("entity_id_8888");
            Dictionary<CursorToken, string> cache = new(CursorToken.OrdinalComparer) {
                [token] = "CachedUtf8Payload"
            };

            var lookup = cache.GetAlternateLookup<ReadOnlySpan<byte>>();
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(token.Value);

            // Act
            bool found = lookup.TryGetValue(utf8Bytes, out string? value);

            // Assert
            Assert.True(found);
            Assert.Equal("CachedUtf8Payload", value);
        }

        [Fact]
        public void Should_Report_Miss_When_Key_Is_Not_Present() {
            // Arrange
            CursorToken existingToken = CursorToken.FromUtf8("entity_id_1111");
            Dictionary<CursorToken, string> cache = new(CursorToken.OrdinalComparer) {
                [existingToken] = "CachedPayload"
            };
            var lookup = cache.GetAlternateLookup<ReadOnlySpan<char>>();

            // Act
            bool found = lookup.TryGetValue("non_existent_key".AsSpan(), out string? value);

            // Assert
            Assert.False(found);
            Assert.Null(value);
        }
    }

    public sealed class ConversionOperators {
        [Fact]
        public void Should_Convert_Implicitly_To_String_And_Span() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("my_cursor_key");

            // Act
            string str = token;
            ReadOnlySpan<char> span = token;

            // Assert
            Assert.Equal(token.Value, str);
            Assert.True(span.SequenceEqual(token.Value.AsSpan()));
        }

        [Fact]
        public void Should_Convert_Explicitly_From_String() {
            // Arrange
            Base64UrlString original = Base64UrlString.FromUtf8("my_cursor_key");

            // Act
            CursorToken token = (CursorToken)original.Value;

            // Assert
            Assert.Equal(original.Value, token.Value);
        }
    }

    public sealed class EqualityOperators {
        [Fact]
        public void Should_Be_Equal_When_Values_Match() {
            // Arrange
            CursorToken token1 = CursorToken.FromUtf8("same_token");
            CursorToken token2 = CursorToken.FromUtf8("same_token");

            // Act & Assert
            Assert.True(token1 == token2);
            Assert.True(token1.Equals(token2));
            Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
        }

        [Fact]
        public void Should_Not_Be_Equal_When_Values_Differ() {
            // Arrange
            CursorToken token1 = CursorToken.FromUtf8("token_a");
            CursorToken token2 = CursorToken.FromUtf8("token_b");

            // Act & Assert
            Assert.True(token1 != token2);
            Assert.False(token1.Equals(token2));
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_Serialize_And_Deserialize_Accurately() {
            // Arrange
            CursorToken original = CursorToken.FromUtf8("token_payload_777");

            // Act
            string json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<CursorToken>(json);

            // Assert
            Assert.Equal(original, deserialized);
            Assert.Equal(original.Value, deserialized.Value);
        }
    }
}