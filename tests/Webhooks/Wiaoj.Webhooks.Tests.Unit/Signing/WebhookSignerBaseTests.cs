using System.Text;
using Wiaoj.Webhooks.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

/// <summary>
/// Comprehensive unit tests for <see cref="WebhookSignerBase"/> covering canonical payload formatting,
/// strict header parsing, DoS resilience, whitespace trimming, and clock tolerance boundaries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Webhooks")]
[Trait("Feature", "SigningRootBase")]
public sealed class WebhookSignerBaseTests {
    private static readonly byte[] TestPayload = "{\"event\":\"order.created\"}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    public sealed class TheConstructorAndProperties {
        [Fact]
        public void Constructor_AcceptsCustomHeaderName() {
            FakeWebhookSigner signer = new("X-Custom-Signature-Header");
            Assert.Equal("X-Custom-Signature-Header", signer.HeaderName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenHeaderNameIsNullOrWhiteSpace(string? invalidHeader) {
            Assert.ThrowsAny<ArgumentException>(() => new FakeWebhookSigner(invalidHeader!));
        }
    }

    public sealed class TheCanonicalPayloadFormatting {
        [Fact]
        public void CreateSignedBytes_FormatsCanonicalFormat_Correctly() {
            FakeWebhookSigner signer = new();
            byte[] result = signer.ExposeCreateSignedBytes(TestPayload, BaseTimestamp);

            string expectedString = $"1700000000.{Encoding.UTF8.GetString(TestPayload)}";
            Assert.Equal(expectedString, Encoding.UTF8.GetString(result));
        }

        [Fact]
        public void CreateSignedBytes_HandlesPayloadsExceeding256ByteStackBuffer() {
            FakeWebhookSigner signer = new();
            byte[] largePayload = new byte[1024];
            Array.Fill(largePayload, (byte)'X');

            byte[] result = signer.ExposeCreateSignedBytes(largePayload, BaseTimestamp);

            Assert.Equal(1024 + 1 + 10, result.Length);
            Assert.StartsWith("1700000000.XXXX", Encoding.UTF8.GetString(result));
        }
    }

    public sealed class TheToleranceAndClockSkew {
        [Theory]
        [InlineData(299, true)]
        [InlineData(300, true)]
        [InlineData(301, false)]
        [InlineData(-300, true)]
        [InlineData(-301, false)]
        public void ValidateVerificationParameters_EnforcesToleranceBoundaries(int secondsOffset, bool expectedResult) {
            FakeWebhookSigner signer = new();
            UnixTimestamp verifyTime = BaseTimestamp + TimeSpan.FromSeconds(secondsOffset);
            string header = $"t={BaseTimestamp.TotalSeconds},v1=valid_sig";

            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), verifyTime);

            Assert.Equal(expectedResult, isValid);
        }

        [Fact]
        public void ValidateVerificationParameters_Throws_WhenToleranceIsNegative() {
            FakeWebhookSigner signer = new();
            string header = $"t={BaseTimestamp.TotalSeconds},v1=valid_sig";

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromSeconds(-1), BaseTimestamp));
        }

        [Fact]
        public void Verify_OverloadWithoutTimestamp_UsesCurrentTime() {
            FakeWebhookSigner signer = new();
            string header = $"t={UnixTimestamp.Now.TotalSeconds},v1=valid_sig";

            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5));

            Assert.True(isValid);
        }
    }

    public sealed class TheHeaderParsingAndWhitespaceEdgeCases {
        [Fact]
        public void TryParseHeader_HandlesWhitespaceAroundSeparatorsAndValues_Correctly() {
            FakeWebhookSigner signer = new();
            string headerWithWhitespace = $"  t={BaseTimestamp.TotalSeconds}  ,   v1=  sig_value_123   ";

            bool isValid = signer.Verify(TestPayload, headerWithWhitespace, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Fact]
        public void TryParseHeader_PreservesBase64PaddingEqualsSigns() {
            FakeWebhookSigner signer = new();
            string base64WithPaddingHeader = $"t={BaseTimestamp.TotalSeconds},v1=dGVzdA==";

            bool isValid = signer.Verify(TestPayload, base64WithPaddingHeader, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Fact]
        public void TryParseHeader_IsCaseInsensitiveForTimestampAndSchemePrefix() {
            FakeWebhookSigner signer = new();
            string uppercaseHeader = $"T={BaseTimestamp.TotalSeconds},V1=sig_value";

            bool isValid = signer.Verify(TestPayload, uppercaseHeader, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Fact]
        public void TryParseHeader_IgnoresOtherSchemes_AndExtractsTargetScheme() {
            FakeWebhookSigner signer = new();
            string mixedHeader = $"t={BaseTimestamp.TotalSeconds},v2=sha512_hash,v1=target_sig,v1_es256=ecdsa_sig";

            bool isValid = signer.Verify(TestPayload, mixedHeader, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData("t=1700000000,v1=")]
        [InlineData("t=1700000000,v1=    ")]
        public void TryParseHeader_ReturnsFalse_WhenSignatureValueIsEmpty(string headerWithEmptySignature) {
            FakeWebhookSigner signer = new();

            bool isValid = signer.Verify(TestPayload, headerWithEmptySignature, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }

    public sealed class TheHeaderParsingAndDoSDefenses {
        [Fact]
        public void TryParseHeader_RejectsMultipleTimestamps_ParameterPollution() {
            FakeWebhookSigner signer = new();
            string pollutedHeader = $"t={BaseTimestamp.TotalSeconds},v1=sig1,t={BaseTimestamp.TotalSeconds + 60}";

            bool isValid = signer.Verify(TestPayload, pollutedHeader, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Fact]
        public void TryParseHeader_HandlesCommaBombDoS_Gracefully() {
            FakeWebhookSigner signer = new();
            string commaBomb = $"t={BaseTimestamp.TotalSeconds}," + new string(',', 5000) + "v1=sig1," + new string(',', 5000);

            bool isValid = signer.Verify(TestPayload, commaBomb, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData("-9223372036854775808")]
        [InlineData("9223372036854775807")]
        [InlineData("-1")]
        [InlineData("not_a_valid_timestamp")]
        [InlineData("1700000000.55")]
        [InlineData("1e10")]
        public void TryParseHeader_HandlesMalformedOrExtremeTimestamps(string extremeTimestamp) {
            FakeWebhookSigner signer = new();
            string header = $"t={extremeTimestamp},v1=sig1";

            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("t=1700000000")]
        [InlineData("v1=sig1")]
        [InlineData(",,,,,,,,")]
        [InlineData("random_invalid_header_content")]
        public void TryParseHeader_ReturnsFalse_WhenHeaderFormatIsInvalid(string invalidHeader) {
            FakeWebhookSigner signer = new();

            bool isValid = signer.Verify(TestPayload, invalidHeader, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }
}