using Wiaoj.Webhooks.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AsymmetricWebhookSignerBase"/> verifying Base64 signature decoding,
/// signature length verification, and zero-allocation engine iteration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Webhooks")]
[Trait("Feature", "AsymmetricSigningBase")]
public sealed class AsymmetricWebhookSignerBaseTests {
    private static readonly byte[] TestPayload = "{\"event\":\"asymmetric.test\"}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    public sealed class TheBase64DecodingAndExecution {
        [Fact]
        public void VerifyAsymmetricCore_ReturnsTrue_WhenValidBase64MatchesVerifier() {
            const int expectedLength = 64;
            byte[] validSignatureBytes = new byte[expectedLength];
            Array.Fill(validSignatureBytes, (byte)0xAB);
            string base64Sig = Convert.ToBase64String(validSignatureBytes);

            FakeAsymmetricWebhookSigner signer = new(
                expectedSignatureLength: expectedLength,
                verifierCallback: (_, signature) => signature.SequenceEqual(validSignatureBytes));

            string header = $"t={BaseTimestamp.TotalSeconds},v1_test={base64Sig}";
            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Fact]
        public void VerifyAsymmetricCore_ReturnsFalse_WhenSignatureByteLengthDoesNotMatchExpected() {
            const int expectedLength = 64;
            byte[] shortSignatureBytes = new byte[32];
            string base64Sig = Convert.ToBase64String(shortSignatureBytes);

            bool verifierInvoked = false;
            FakeAsymmetricWebhookSigner signer = new(
                expectedSignatureLength: expectedLength,
                verifierCallback: (_, _) => {
                    verifierInvoked = true;
                    return true;
                });

            string header = $"t={BaseTimestamp.TotalSeconds},v1_test={base64Sig}";
            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
            Assert.False(verifierInvoked);
        }

        [Theory]
        [InlineData("!!!not_base64!!!")]
        [InlineData("===")]
        [InlineData("")]
        public void VerifyAsymmetricCore_ReturnsFalse_WhenBase64IsMalformed_WithoutThrowing(string malformedBase64) {
            FakeAsymmetricWebhookSigner signer = new(expectedSignatureLength: 64);
            string header = $"t={BaseTimestamp.TotalSeconds},v1_test={malformedBase64}";

            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }

    public sealed class TheMultiSignatureAndZeroDowntimeRotation {
        [Fact]
        public void VerifyAsymmetricCore_FindsValidSignature_WhenMultipleCandidatesProvided() {
            const int expectedLength = 64;
            byte[] validSignatureBytes = new byte[expectedLength];
            Array.Fill(validSignatureBytes, (byte)0xFE);

            byte[] invalidSignatureBytes = new byte[expectedLength];
            Array.Fill(invalidSignatureBytes, (byte)0x01);

            string validBase64 = Convert.ToBase64String(validSignatureBytes);
            string invalidBase64 = Convert.ToBase64String(invalidSignatureBytes);

            FakeAsymmetricWebhookSigner signer = new(
                expectedSignatureLength: expectedLength,
                verifierCallback: (_, signature) => signature.SequenceEqual(validSignatureBytes));

            string multiSigHeader = $"t={BaseTimestamp.TotalSeconds},v1_test={invalidBase64},v1_test={validBase64}";
            bool isValid = signer.Verify(TestPayload, multiSigHeader, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }
    }

    public sealed class TheBufferScalingAndLargeSignatures {
        [Fact]
        public void VerifyAsymmetricCore_HandlesSignaturesExceeding512ByteStackBuffer() {
            const int largeSignatureLength = 1024;
            byte[] largeSignature = new byte[largeSignatureLength];
            Array.Fill(largeSignature, (byte)0x42);
            string base64Sig = Convert.ToBase64String(largeSignature);

            FakeAsymmetricWebhookSigner signer = new(
                expectedSignatureLength: largeSignatureLength,
                verifierCallback: (_, signature) => signature.SequenceEqual(largeSignature));

            string header = $"t={BaseTimestamp.TotalSeconds},v1_test={base64Sig}";
            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }
    }
}