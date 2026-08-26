namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Rsa;

/// <summary>
/// Unit tests verifying RSA cryptographic resilience against bit-level mutations, signature truncation, and padding attacks.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "RsaAttacks")]
public sealed class RsaCryptographicAttackTests {
    private static readonly byte[] CanonicalPayload = "{\"orderId\":\"ORD-888999\",\"amount\":1500.00,\"currency\":\"USD\"}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    public sealed class PayloadAndSignatureTampering {
        [Fact]
        public void Verify_ReturnsFalse_WhenSingleBitIsFlippedInPayload() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] tamperedPayload = (byte[])CanonicalPayload.Clone();
            tamperedPayload[^2] ^= 0b0000_0001;

            bool isValid = signer.Verify(tamperedPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenTrailingNullByteIsAppendedToPayload() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);
            byte[] nullInjectedPayload = [.. CanonicalPayload, 0x00];

            bool isValid = signer.Verify(nullInjectedPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSingleCharIsMutatedInBase64Signature() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            char[] sigChars = signature.Signature.ToCharArray();
            sigChars[10] = sigChars[10] == 'A' ? 'B' : 'A';
            string tamperedHeader = $"t={BaseTimestamp.TotalSeconds},v1_ps256={new string(sigChars)}";

            bool isValid = signer.Verify(CanonicalPayload, tamperedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }

    public sealed class TruncatedAndMalformedSignatures {
        [Fact]
        public void Verify_ReturnsFalse_WhenSignatureIsTruncatedBySingleByte() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            byte[] truncatedSig = rawSig[..^1];
            string truncatedHeader = $"t={BaseTimestamp.TotalSeconds},v1_ps256={Convert.ToBase64String(truncatedSig)}";

            bool isValid = signer.Verify(CanonicalPayload, truncatedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }
}