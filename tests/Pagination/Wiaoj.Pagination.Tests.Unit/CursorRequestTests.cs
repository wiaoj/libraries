namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "KeysetPagination")]
public sealed class CursorRequestTests {
    // Valid Base64Url token string generated safely
    private static readonly string ValidTokenString = CursorToken.FromUtf8("cursor_payload").Value;

    public sealed class Constructor {
        [Theory]
        [InlineData(int.MinValue, CursorRequest.DefaultLimit)]
        [InlineData(-5, CursorRequest.DefaultLimit)]
        [InlineData(0, CursorRequest.DefaultLimit)]
        [InlineData(1, 1)]
        [InlineData(50, 50)]
        [InlineData(CursorRequest.MaxLimit, CursorRequest.MaxLimit)]
        [InlineData(CursorRequest.MaxLimit + 1, CursorRequest.MaxLimit)]
        [InlineData(int.MaxValue, CursorRequest.MaxLimit)]
        public void Should_Clamp_Limit_Boundaries(int inputLimit, int expectedLimit) {
            // Arrange
            CursorToken token = CursorToken.Parse(ValidTokenString);

            // Act
            CursorRequest sut = new(token, inputLimit, CursorDirection.Forward);

            // Assert
            Assert.Equal(expectedLimit, sut.Limit);
        }
    }

    public sealed class ParseMethod {
        [Fact]
        public void Should_Parse_Token_Only_Format() {
            // Arrange
            string input = ValidTokenString;

            // Act
            CursorRequest result = CursorRequest.Parse(input);

            // Assert
            Assert.Equal(input, result.Cursor.Value);
            Assert.Equal(CursorRequest.DefaultLimit, result.Limit);
            Assert.Equal(CursorDirection.Forward, result.Direction);
        }

        [Fact]
        public void Should_Parse_Token_And_Limit_Format() {
            // Arrange
            string input = $"{ValidTokenString}:50";

            // Act
            CursorRequest result = CursorRequest.Parse(input);

            // Assert
            Assert.Equal(ValidTokenString, result.Cursor.Value);
            Assert.Equal(50, result.Limit);
            Assert.Equal(CursorDirection.Forward, result.Direction);
        }

        [Theory]
        [InlineData("Forward", CursorDirection.Forward)]
        [InlineData("forward", CursorDirection.Forward)]
        [InlineData("FORWARD", CursorDirection.Forward)]
        [InlineData("Backward", CursorDirection.Backward)]
        [InlineData("backward", CursorDirection.Backward)]
        public void Should_Parse_Full_Format_With_Case_Insensitive_Direction(string directionStr, CursorDirection expectedDirection) {
            // Arrange
            string input = $"{ValidTokenString}:25:{directionStr}";

            // Act
            CursorRequest result = CursorRequest.Parse(input);

            // Assert
            Assert.Equal(ValidTokenString, result.Cursor.Value);
            Assert.Equal(25, result.Limit);
            Assert.Equal(expectedDirection, result.Direction);
        }

        [Theory]
        [InlineData(":")]
        [InlineData("::")]
        [InlineData("invalid!token:50:Forward")]
        [InlineData("invalid_direction:50:NotADirection")]
        [InlineData("valid_token:999999999999999999999:Forward")]
        public void Should_Fail_Gracefully_On_Malformed_Inputs(string malformed) {
            // Act & Assert
            Assert.False(CursorRequest.TryParse(malformed, out _));
            Assert.Throws<FormatException>(() => CursorRequest.Parse(malformed));
        }

        [Fact]
        public void Should_Return_Default_Instance_When_Span_Is_Empty() {
            // Act
            bool success = CursorRequest.TryParse(ReadOnlySpan<char>.Empty, out CursorRequest result);

            // Assert
            Assert.True(success);
            Assert.Equal(CursorRequest.Default, result);
        }
    }

    public sealed class TryFormatMethod {
        [Fact]
        public void Should_Format_Default_Struct_Safely() {
            // Arrange
            CursorRequest sut = default;
            Span<char> destination = stackalloc char[64];

            // Act
            bool success = sut.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal("Cursor: [None], Limit: 0, Direction: Forward", destination[..charsWritten].ToString());
        }

        [Fact]
        public void Should_Return_False_When_Buffer_Is_Insufficient() {
            // Arrange
            CursorRequest sut = new(CursorToken.Parse(ValidTokenString), 25, CursorDirection.Forward);
            Span<char> smallBuffer = stackalloc char[3];

            // Act
            bool success = sut.TryFormat(smallBuffer, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }
    }
}