using System.Text;
using Wiaoj.Webhooks.Signing.Asymmetric.Ed25519;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Ed25519;

[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "Ed25519")]
public sealed class Ed25519WebhookSignerTests {
    private static readonly byte[] TestPayload = "{\"event\":\"order.created\",\"total\":149.90}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR, PROPERTIES & SCHEME PREFIXES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructorAndProperties {
        [ExperimentalFact]
        public void DefaultConstructor_InitializesWithEd25519AndV1aScheme() {
            Ed25519WebhookSigner signer = new();

            Assert.Equal("ed25519", signer.AlgorithmName);
            Assert.Equal("Webhook-Signature", signer.HeaderName);
            Assert.Equal("v1a", signer.SchemePrefix);
        }

        [ExperimentalFact]
        public void Constructor_AcceptsCustomHeaderAndSchemePrefix() {
            Ed25519WebhookSigner custom = new(headerName: "X-Custom-Ed25519", schemePrefix: "ed25519");

            Assert.Equal("X-Custom-Ed25519", custom.HeaderName);
            Assert.Equal("ed25519", custom.SchemePrefix);
            Assert.Equal("ed25519", custom.AlgorithmName);
        }

         [ExperimentalTheory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenHeaderNameIsNullOrWhiteSpace(string? invalidHeader) {
            Assert.ThrowsAny<ArgumentException>(() => new Ed25519WebhookSigner(invalidHeader!, "v1a"));
        }

         [ExperimentalTheory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenSchemePrefixIsNullOrWhiteSpace(string? invalidScheme) {
            Assert.ThrowsAny<ArgumentException>(() => new Ed25519WebhookSigner("Webhook-Signature", invalidScheme!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. SIGN AND VERIFY LIFECYCLE (KEYPAIR, PUBLIC KEY & UNMANAGED MEMORY)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSignAndVerifyFlow {
        [ExperimentalFact]
        public void SignAndVerify_WithKeyPairAndPublicKey_Succeeds() {
            // Arrange: Generate RFC 8032 Curve25519 KeyPair
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            // Act: Sign payload with Private Key (Seed)
            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            // Assert: Standard Webhooks format "t=1700000000,v1a=<Base64>"
            Assert.StartsWith($"t={TestTime.TotalSeconds},v1a=", signature.HeaderValue);

            // Act: Verify using strictly the 32-byte Public Key
            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [ExperimentalFact]
        public void SignAndVerify_WithRaw32BytePublicKeySpan_Succeeds() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            byte[] rawPublicKeyBytes = keyPair.PublicKey.ToByteArray();

            // IWebhookSigner standard interface overload with raw byte span
            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                rawPublicKeyBytes,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [ExperimentalFact]
        public void SignAndVerify_UsingUnmanagedSecretKey_SucceedsWithoutMemoryLeaks() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            // Export private seed to unmanaged memory Secret<byte>
            using Secret<byte> seedSecret = keyPair.ExportPrivateKeySeed();

            // Act: Sign via Secret<byte> overload
            WebhookSignature signature = signer.Sign(TestPayload, seedSecret, TestTime);

            // Assert: Verify against public key
            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenVerifiedWithWrongPublicKey() {
            using Ed25519KeyPair signerKey = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            using Ed25519KeyPair attackerKey = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(TestPayload, signerKey, TestTime);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                attackerKey.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenPayloadIsTampered() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            byte[] tampered = "{\"event\":\"order.created\",\"total\":999.90}"u8.ToArray();

            bool isValid = signer.Verify(
                tampered,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }

        [ExperimentalFact]
        public void Verify_ReturnsFalse_WhenSchemePrefixMismatch_V1aVsV1() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner edSigner = new(headerName: "Webhook-Signature", schemePrefix: "v1a");
            Ed25519WebhookSigner otherSigner = new(headerName: "Webhook-Signature", schemePrefix: "ed25519_custom");

            WebhookSignature signature = edSigner.Sign(TestPayload, keyPair, TestTime);

            // Verified against a signer expecting different scheme prefix
            bool isValid = otherSigner.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. ZERO-DOWNTIME KEY ROTATION (MULTI-SIGNATURE HEADERS)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheKeyRotationFlow {
        [ExperimentalFact]
        public void Verify_Succeeds_WhenHeaderContainsBothOldAndNewSignatures() {
            // Arrange: Zero-downtime key rotation scenario
            using Ed25519KeyPair oldKeyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            using Ed25519KeyPair newKeyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            WebhookSignature oldSig = signer.Sign(TestPayload, oldKeyPair, TestTime);
            WebhookSignature newSig = signer.Sign(TestPayload, newKeyPair, TestTime);

            string multiSigHeader = $"t={TestTime.TotalSeconds},v1a={oldSig.Signature},v1a={newSig.Signature}";

            // Assert: Both old and new public keys can verify the multi-sig header
            Assert.True(signer.Verify(TestPayload, multiSigHeader, oldKeyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
            Assert.True(signer.Verify(TestPayload, multiSigHeader, newKeyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. PAYLOAD EDGE-CASES & UNICODE
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ThePayloadEdgeCases {
        [ExperimentalFact]
        public void SignAndVerify_Succeeds_WithEmptyPayload() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();
            byte[] empty = [];

            WebhookSignature signature = signer.Sign(empty, keyPair, TestTime);
            Assert.True(signer.Verify(empty, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }

        [ExperimentalFact]
        public void SignAndVerify_Succeeds_WithComplexUnicodeAndEmojis() {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();
            byte[] unicodePayload = Encoding.UTF8.GetBytes("{\"text\":\"Ed25519 İmzası 🚀 Türkçe: ğüşıöç 🌍 / 中文\"}");

            WebhookSignature signature = signer.Sign(unicodePayload, keyPair, TestTime);
            Assert.True(signer.Verify(unicodePayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }

         [ExperimentalTheory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid_header")]
        [InlineData("t=1700000000")]
        [InlineData("v1a=abc")]
        public void Verify_ReturnsFalse_WhenHeaderIsMalformed(string malformedHeader) {
            using Ed25519KeyPair keyPair = Ed25519KeyPair.Generate(Ed25519PublicKey.Create);
            Ed25519WebhookSigner signer = new();

            bool isValid = signer.Verify(TestPayload, malformedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }
    }
}