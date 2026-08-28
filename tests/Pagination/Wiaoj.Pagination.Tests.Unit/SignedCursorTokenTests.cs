using System.Text.Json;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "KeysetPagination")]
public sealed class SignedCursorTokenTests {
    private static readonly byte[] ValidSecretKey = "super_secret_signing_key_32bytes!"u8.ToArray();
    private static readonly byte[] WrongSecretKey = "another_different_secret_key_32b!"u8.ToArray();

    public sealed class SignMethod {
        [Fact]
        public void Should_Sign_Payload_With_Valid_Key() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("order_id_109520");

            // Act
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Assert
            Assert.False(signedToken.IsEmpty);
            Assert.Equal(rawToken, signedToken.Token);
            Assert.True(signedToken.Verify(ValidSecretKey));
        }

        [Fact]
        public void Should_Return_Empty_When_Token_Is_Empty() {
            // Arrange & Act
            SignedCursorToken signed = SignedCursorToken.Sign(CursorToken.Empty, ValidSecretKey);

            // Assert
            Assert.True(signed.IsEmpty);
            Assert.Equal(SignedCursorToken.Empty, signed);
        }
    }

    public sealed class VerifyMethod {
        [Fact]
        public void Should_Fail_When_Key_Is_Invalid() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("order_id_109520");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Act
            bool isValid = signedToken.Verify(WrongSecretKey);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void Should_Fail_When_Payload_Is_Tampered() {
            // Arrange
            CursorToken originalToken = CursorToken.FromUtf8("original_payload");
            SignedCursorToken signedToken = SignedCursorToken.Sign(originalToken, ValidSecretKey);

            SignedCursorToken tamperedToken = new(CursorToken.FromUtf8("tampered_payload"), signedToken.Signature);

            // Act & Assert
            Assert.False(tamperedToken.Verify(ValidSecretKey));
        }
    }

    public sealed class TryUnsignMethod {
        [Fact]
        public void Should_Extract_Original_Token_When_Key_Is_Valid() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("target_order");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Act
            bool success = signedToken.TryUnsign(ValidSecretKey, out CursorToken extractedToken);

            // Assert
            Assert.True(success);
            Assert.Equal(rawToken, extractedToken);
        }

        [Fact]
        public void Should_Return_False_And_Empty_Token_When_Key_Is_Invalid() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("target_order");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Act
            bool success = signedToken.TryUnsign(WrongSecretKey, out CursorToken extractedToken);

            // Assert
            Assert.False(success);
            Assert.Equal(CursorToken.Empty, extractedToken);
        }
    }

    public sealed class ParseMethod {
        [Fact]
        public void Should_Parse_Formatted_Token() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("cursor_data_12345");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Act
            string formatted = signedToken.ToString();
            SignedCursorToken parsed = SignedCursorToken.Parse(formatted);

            // Assert
            Assert.Equal(signedToken, parsed);
            Assert.True(parsed.Verify(ValidSecretKey));
        }

        [Theory]
        [InlineData("invalid_without_dot")]
        [InlineData(".only_signature")]
        [InlineData("only_payload.")]
        [InlineData("")]
        public void Should_Fail_Parsing_Malformed_Strings(string malformed) {
            // Act & Assert
            Assert.False(SignedCursorToken.TryParse(malformed, out _));
            Assert.Throws<FormatException>(() => SignedCursorToken.Parse(malformed));
        }
    }

    public sealed class TryFormatMethod {
        [Fact]
        public void Should_Format_To_Char_Span() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("cursor_payload");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);
            Span<char> destination = stackalloc char[128];

            // Act
            bool success = signedToken.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(signedToken.ToString(), destination[..charsWritten].ToString());
        }

        [Fact]
        public void Should_Return_False_When_Buffer_Is_Too_Small() {
            // Arrange
            SignedCursorToken signed = SignedCursorToken.Sign(CursorToken.FromUtf8("valid_payload"), ValidSecretKey);
            Span<char> smallBuffer = stackalloc char[10];

            // Act
            bool success = signed.TryFormat(smallBuffer, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }
    }

    public sealed class EmptyProperty {
        [Fact]
        public void Should_Represent_Default_State() {
            // Arrange & Act
            SignedCursorToken sut = SignedCursorToken.Empty;

            // Assert
            Assert.True(sut.IsEmpty);
            Assert.True(sut.Token.IsEmpty);
            Assert.False(sut.Verify(ValidSecretKey));
            Assert.Equal(string.Empty, sut.ToString());
        }
    }

    public sealed class ConversionOperators {
        [Fact]
        public void Should_Convert_Implicitly_To_String() {
            // Arrange
            SignedCursorToken original = SignedCursorToken.Sign(CursorToken.FromUtf8("conversion_test"), ValidSecretKey);

            // Act
            string str = original;

            // Assert
            Assert.Equal(original.ToString(), str);
        }

        [Fact]
        public void Should_Convert_Explicitly_From_String() {
            // Arrange
            SignedCursorToken original = SignedCursorToken.Sign(CursorToken.FromUtf8("conversion_test"), ValidSecretKey);

            // Act
            SignedCursorToken explicitParsed = (SignedCursorToken)original.ToString();

            // Assert
            Assert.Equal(original, explicitParsed);
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_Serialize_And_Deserialize_Accurately() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("json_signed_test");
            SignedCursorToken original = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Act
            string json = JsonSerializer.Serialize(original);
            SignedCursorToken deserialized = JsonSerializer.Deserialize<SignedCursorToken>(json);

            // Assert
            Assert.Equal(original, deserialized);
            Assert.True(deserialized.Verify(ValidSecretKey));
        }
    }
}