namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Ecdsa;

/// <summary>
/// Unit tests verifying ECDSA cryptographic resilience against truncated signatures and bit-level mutations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "EcdsaAttacks")]
public sealed class EcdsaCryptographicAttackTests {
    private static readonly byte[] CanonicalPayload = "{\"paymentId\":\"PAY-777\",\"amount\":49.99}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    public sealed class TruncatedAndMutatedSignatures {
        [Fact]
        public void Verify_ReturnsFalse_WhenEcdsaP256SignatureIsTruncatedTo63Bytes() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            byte[] truncatedSig = rawSig[..63];
            string truncatedHeader = $"t={BaseTimestamp.TotalSeconds},v1_es256={Convert.ToBase64String(truncatedSig)}";

            bool isValid = signer.Verify(CanonicalPayload, truncatedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSingleBitMutatedInPayload() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] tampered = (byte[])CanonicalPayload.Clone();
            tampered[5] ^= 0xFF;

            bool isValid = signer.Verify(tampered, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenTrailingWhitespaceInjectedInPayload() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);
            byte[] whitespacePayload = [.. CanonicalPayload, (byte)' '];

            bool isValid = signer.Verify(whitespacePayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }
}