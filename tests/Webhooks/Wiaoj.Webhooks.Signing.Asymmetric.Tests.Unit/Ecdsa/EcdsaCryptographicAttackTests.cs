using System.Text;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Ecdsa;

[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "EcdsaAttacks")]
public sealed class EcdsaCryptographicAttackTests {
    private static readonly byte[] CanonicalPayload = "{\"paymentId\":\"PAY-777\",\"amount\":49.99}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    // ────────────────────────────────────────────────────────────────────────
    // 1. TRUNCATED & MUTATED SIGNATURES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TruncatedAndMutatedSignatures {
        [Fact]
        public void Verify_ReturnsFalse_WhenEcdsaP256SignatureIsTruncatedTo63Bytes() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // P-256 signature is exactly 64 bytes. Truncate to 63 bytes.
            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            byte[] truncatedSig = rawSig[..63];
            string truncatedHeader = $"t={BaseTimestamp.TotalSeconds},v1_es256={Convert.ToBase64String(truncatedSig)}";

            bool isValid = signer.Verify(CanonicalPayload, truncatedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Security flaw: 63-byte truncated ECDSA signature was accepted!");
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSingleBitMutatedInPayload() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] tampered = (byte[])CanonicalPayload.Clone();
            tampered[5] ^= 0xFF; // Flip byte

            bool isValid = signer.Verify(tampered, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenTrailingWhitespaceInjectedInPayload() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // Injected trailing whitespace
            byte[] whitespacePayload = [.. CanonicalPayload, (byte)' '];

            bool isValid = signer.Verify(whitespacePayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Security flaw: Injected trailing whitespace was not detected!");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. ZERO-DOWNTIME KEY ROTATION (MULTI-SIGNATURE HEADERS)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class KeyRotationAndDualSignatures {
        [Fact]
        public void Verify_Succeeds_WhenHeaderContainsBothOldAndNewSignatures() {
            using EcdsaKeyPair oldKey = EcdsaKeyPair.GenerateP256();
            using EcdsaKeyPair newKey = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature oldSig = signer.Sign(CanonicalPayload, oldKey, BaseTimestamp);
            WebhookSignature newSig = signer.Sign(CanonicalPayload, newKey, BaseTimestamp);

            // Dual signature header (e.g. during active provider key rotation)
            string multiSigHeader = $"t={BaseTimestamp.TotalSeconds},v1_es256={oldSig.Signature},v1_es256={newSig.Signature}";

            // Assert: Both keys can successfully verify against the same multi-sig header
            Assert.True(signer.Verify(CanonicalPayload, multiSigHeader, oldKey.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp));
            Assert.True(signer.Verify(CanonicalPayload, multiSigHeader, newKey.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. REPLAY TOLERANCE & ATTACK DEFENSES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ReplayAndDoSDefenses {
        [Fact]
        public void Verify_HandlesCommaBombDoS_Gracefully() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);
            string commaBomb = $"t={BaseTimestamp.TotalSeconds}," + new string(',', 3000) + $"v1_es256={signature.Signature}," + new string(',', 3000);

            bool isValid = signer.Verify(CanonicalPayload, commaBomb, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData(299, true)]
        [InlineData(301, false)]
        public void Verify_EnforcesExactToleranceThresholds(int secondsOffset, bool expected) {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);
            UnixTimestamp verifyTime = BaseTimestamp + TimeSpan.FromSeconds(secondsOffset);

            bool isValid = signer.Verify(CanonicalPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), verifyTime);

            Assert.Equal(expected, isValid);
        }
    }
}