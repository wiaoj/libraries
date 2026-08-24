using System.Text;
using Wiaoj.Webhooks.Signing.Asymmetric.Ed25519;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Ed25519;

[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "Ed25519Attacks")]
public sealed class Ed25519CryptographicAttackTests {
    private static readonly byte[] CanonicalPayload = "{\"event\":\"order.created\",\"amount\":99.95}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    public sealed class TruncatedAndMutatedSignatures {
        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenEd25519SignatureIsTruncatedTo63Bytes() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // Ed25519 signature is strictly 64 bytes. Truncate to 63 bytes.
            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            byte[] truncatedSig = rawSig[..63];
            string truncatedHeader = $"t={BaseTimestamp.TotalSeconds},v1a={Convert.ToBase64String(truncatedSig)}";

            bool isValid = signer.Verify(CanonicalPayload, truncatedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Security flaw: 63-byte truncated Ed25519 signature was accepted!");
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenSingleBitMutatedInPayload() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] tampered = (byte[])CanonicalPayload.Clone();
            tampered[10] ^= 0x01; // Flip single bit

            bool isValid = signer.Verify(tampered, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenTrailingNullByteInjectedInPayload() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            byte[] nullInjected = [.. CanonicalPayload, 0x00];

            bool isValid = signer.Verify(nullInjected, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }

    public sealed class TheNullAndBoundaryGuards {
        [ExperimentalFact]
        public void Sign_Throws_WhenKeyPairIsNull() {
            Ed25519WebhookSigner signer = new();
            Assert.ThrowsAny<ArgumentNullException>(() =>
                signer.Sign(CanonicalPayload, (Ed25519KeyPair)null!, BaseTimestamp));
        }

        [ExperimentalFact]
        public void Sign_Throws_WhenSecretKeyIsDefault() {
            Ed25519WebhookSigner signer = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                signer.Sign(CanonicalPayload, default(Secret<byte>), BaseTimestamp));
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenPublicKeyIsEmpty() {
            Ed25519WebhookSigner signer = new();
            bool isValid = signer.Verify(CanonicalPayload, "t=1700000000,v1a=abc", default(Ed25519PublicKey), TimeSpan.FromMinutes(5), BaseTimestamp);
            Assert.False(isValid);
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenSecretKeyIsDefault() {
            Ed25519WebhookSigner signer = new();
            bool isValid = signer.Verify(CanonicalPayload, "t=1700000000,v1a=abc", default(Secret<byte>), TimeSpan.FromMinutes(5), BaseTimestamp);
            Assert.False(isValid);
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenSecretKeySpanIsEmpty() {
            Ed25519WebhookSigner signer = new();
            bool isValid = signer.Verify(CanonicalPayload, "t=1700000000,v1a=abc", ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), BaseTimestamp);
            Assert.False(isValid);
        }

        [ExperimentalTheory]
        [InlineData(299, true)]   // Within 5m tolerance
        [InlineData(300, true)]   // Exact boundary
        [InlineData(301, false)]  // Expired
        [InlineData(-301, false)] // Future clock drift
        public void Verify_EnforcesExactToleranceThresholds(int secondsOffset, bool expected) {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);
            UnixTimestamp verifyTime = BaseTimestamp + TimeSpan.FromSeconds(secondsOffset);

            bool isValid = signer.Verify(CanonicalPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), verifyTime);

            Assert.Equal(expected, isValid);
        }
    }
}