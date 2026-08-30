using System.Text.Json;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Hashing;

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
        public void Should_Sign_With_Explicit_DateTimeOffset_Through_Implicit_Conversion() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("order_id_109520");
            DateTimeOffset explicitTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

            // Act: Passes DateTimeOffset directly into UnixTimestamp parameter
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey, explicitTime);

            // Assert
            Assert.Equal(UnixTimestamp.From(explicitTime), signedToken.Timestamp);
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

        [Fact]
        public void Should_Throw_When_SecretKey_Is_Null() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("order_id_1");

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => SignedCursorToken.Sign(rawToken, null!));
        }

        [Fact]
        public void Should_Throw_When_SecretKey_Is_Too_Short() {
            // Arrange
            byte[] tooShortKey = "short_key"u8.ToArray();
            CursorToken rawToken = CursorToken.FromUtf8("order_id_1");

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => SignedCursorToken.Sign(rawToken, tooShortKey));
        }
    }

    public sealed class Determinism {
        [Fact]
        public void Should_Produce_Same_Signature_For_Same_Token_Timestamp_And_Key() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("deterministic_payload");
            UnixTimestamp fixedTimestamp = UnixTimestamp.FromMilliseconds(1767268800000);

            // Act
            SignedCursorToken first = SignedCursorToken.Sign(token, ValidSecretKey, fixedTimestamp);
            SignedCursorToken second = SignedCursorToken.Sign(token, ValidSecretKey, fixedTimestamp);

            // Assert
            Assert.Equal(first.Signature, second.Signature);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Should_Produce_Different_Signatures_For_Different_Tokens() {
            // Arrange
            UnixTimestamp fixedTimestamp = UnixTimestamp.FromMilliseconds(1767268800000);

            // Act
            SignedCursorToken signedA = SignedCursorToken.Sign(CursorToken.FromUtf8("payload_a"), ValidSecretKey, fixedTimestamp);
            SignedCursorToken signedB = SignedCursorToken.Sign(CursorToken.FromUtf8("payload_b"), ValidSecretKey, fixedTimestamp);

            // Assert
            Assert.NotEqual(signedA.Signature, signedB.Signature);
        }

        [Fact]
        public void Should_Produce_Different_Signatures_For_Same_Token_With_Different_Keys() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("shared_payload");
            UnixTimestamp fixedTimestamp = UnixTimestamp.FromMilliseconds(1767268800000);

            // Act
            SignedCursorToken signedWithFirstKey = SignedCursorToken.Sign(token, ValidSecretKey, fixedTimestamp);
            SignedCursorToken signedWithSecondKey = SignedCursorToken.Sign(token, WrongSecretKey, fixedTimestamp);

            // Assert
            Assert.NotEqual(signedWithFirstKey.Signature, signedWithSecondKey.Signature);
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

            SignedCursorToken tamperedToken = new(CursorToken.FromUtf8("tampered_payload"), signedToken.Timestamp, signedToken.Signature);

            // Act & Assert
            Assert.False(tamperedToken.Verify(ValidSecretKey));
        }

        [Fact]
        public void Should_Fail_When_Timestamp_Is_Tampered() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("intact_payload");
            SignedCursorToken signedToken = SignedCursorToken.Sign(token, ValidSecretKey);

            // Manipulate timestamp by 1 millisecond
            SignedCursorToken tamperedToken = new(token, signedToken.Timestamp.AddMilliseconds(1), signedToken.Signature);

            // Act & Assert
            Assert.False(tamperedToken.Verify(ValidSecretKey));
        }

        [Fact]
        public void Should_Fail_When_Signature_Itself_Is_Tampered() {
            // Arrange
            CursorToken token = CursorToken.FromUtf8("intact_payload");
            SignedCursorToken signedToken = SignedCursorToken.Sign(token, ValidSecretKey);

            byte[] corruptedSignature = signedToken.Signature.AsSpan().ToArray();
            corruptedSignature[0] ^= 0xFF;
            SignedCursorToken tampered = new(token, signedToken.Timestamp, HmacSha256Hash.FromBytes(corruptedSignature));

            // Act & Assert
            Assert.False(tampered.Verify(ValidSecretKey));
        }

        [Fact]
        public void Should_Fail_Verification_When_Signed_Token_Lifetime_Has_Expired() {
            // Arrange: Token issued 2 hours ago
            UnixTimestamp twoHoursAgo = UnixTimestamp.Now.AddHours(-2);
            CursorToken token = CursorToken.FromUtf8("order_id_109520");
            SignedCursorToken signedToken = SignedCursorToken.Sign(token, ValidSecretKey, twoHoursAgo);

            // Act: Verify with 1-hour maxAge limit
            bool isValid = signedToken.Verify(ValidSecretKey, maxAge: TimeSpan.FromHours(1));

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void Should_Pass_Verification_When_Signed_Token_Is_Within_Lifetime() {
            // Arrange: Token issued 5 minutes ago
            UnixTimestamp fiveMinutesAgo = UnixTimestamp.Now.AddMinutes(-5);
            CursorToken token = CursorToken.FromUtf8("order_id_109520");
            SignedCursorToken signedToken = SignedCursorToken.Sign(token, ValidSecretKey, fiveMinutesAgo);

            // Act: Verify with 1-hour maxAge limit
            bool isValid = signedToken.Verify(ValidSecretKey, maxAge: TimeSpan.FromHours(1));

            // Assert
            Assert.True(isValid);
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
        public void Should_Extract_Original_Token_When_Within_MaxAge() {
            // Arrange
            CursorToken rawToken = CursorToken.FromUtf8("target_order");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey);

            // Act
            bool success = signedToken.TryUnsign(ValidSecretKey, maxAge: TimeSpan.FromHours(1), out CursorToken extractedToken);

            // Assert
            Assert.True(success);
            Assert.Equal(rawToken, extractedToken);
        }

        [Fact]
        public void Should_Return_False_And_Empty_Token_When_Expired() {
            // Arrange
            UnixTimestamp twoHoursAgo = UnixTimestamp.Now.AddHours(-2);
            CursorToken rawToken = CursorToken.FromUtf8("target_order");
            SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, ValidSecretKey, twoHoursAgo);

            // Act
            bool success = signedToken.TryUnsign(ValidSecretKey, maxAge: TimeSpan.FromHours(1), out CursorToken extractedToken);

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
        [InlineData("payload.invalid_timestamp.signature")]
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
            Span<char> destination = stackalloc char[160];

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