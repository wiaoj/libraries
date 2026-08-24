using System.Text;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Ecdsa;

[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "ECDSA")]
public sealed class EcdsaWebhookSignerTests {
    private static readonly byte[] TestPayload = "{\"event\":\"payment.captured\",\"amount\":42.00}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR, PROPERTIES & SCHEME PREFIXES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructorAndProperties {
        [Fact]
        public void DefaultConstructor_InitializesWithEs256AndStandardHeader() {
            EcdsaWebhookSigner signer = new();

            Assert.Equal(EcdsaAlgorithm.ES256, signer.Algorithm);
            Assert.Equal("ecdsa-es256", signer.AlgorithmName);
            Assert.Equal("Webhook-Signature", signer.HeaderName);
            Assert.Equal("v1_es256", signer.SchemePrefix);
        }

        [Theory]
        [InlineData("ES256", "ecdsa-es256", "v1_es256")]
        [InlineData("ES384", "ecdsa-es384", "v1_es384")]
        [InlineData("ES512", "ecdsa-es512", "v1_es512")]
        public void Constructor_SetsExpectedAlgorithmNameAndSchemePrefix(
            string algorithmName,
            string expectedAlgorithmName,
            string expectedSchemePrefix) {

            EcdsaAlgorithm algorithm = algorithmName switch {
                "ES256" => EcdsaAlgorithm.ES256,
                "ES384" => EcdsaAlgorithm.ES384,
                "ES512" => EcdsaAlgorithm.ES512,
                _ => throw new ArgumentException("Unknown algorithm", nameof(algorithmName))
            };

            EcdsaWebhookSigner signer = new(algorithm);

            Assert.Equal(expectedAlgorithmName, signer.AlgorithmName);
            Assert.Equal(expectedSchemePrefix, signer.SchemePrefix);
        }

        [Fact]
        public void Constructor_Throws_WhenAlgorithmIsNull() {
            Assert.ThrowsAny<ArgumentNullException>(() => new EcdsaWebhookSigner(null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenHeaderNameIsNullOrWhiteSpace(string? invalidHeader) {
            Assert.ThrowsAny<ArgumentException>(() => new EcdsaWebhookSigner(EcdsaAlgorithm.ES256, invalidHeader!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. CURVE MATRIX (P-256, P-384, P-521)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheCurveMatrix {
        [Fact]
        public void SignAndVerify_WithNistP256_Succeeds() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            // P-256 IEEE P1363 signature is exactly 64 bytes
            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            Assert.Equal(64, rawSig.Length);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_WithNistP384_Succeeds() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP384();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES384);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            // P-384 IEEE P1363 signature is exactly 96 bytes
            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            Assert.Equal(96, rawSig.Length);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_WithNistP521_Succeeds() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP521();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES512);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            // P-521 IEEE P1363 signature is exactly 132 bytes
            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            Assert.Equal(132, rawSig.Length);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. SECURITY GUARDS, TAMPERING, REPLAY & ROTATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSecurityGuards {
        [Fact]
        public void Verify_ReturnsFalse_WhenPayloadIsTampered() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            byte[] tampered = "{\"event\":\"payment.captured\",\"amount\":9999.00}"u8.ToArray();

            bool isValid = signer.Verify(
                tampered,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenVerifiedWithWrongPublicKey() {
            using EcdsaKeyPair signerKey = EcdsaKeyPair.GenerateP256();
            using EcdsaKeyPair attackerKey = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, signerKey, TestTime);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                attackerKey.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_SupportsMultiSignatureHeader_ForZeroDowntimeKeyRotation() {
            using EcdsaKeyPair oldKeyPair = EcdsaKeyPair.GenerateP256();
            using EcdsaKeyPair newKeyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature oldSig = signer.Sign(TestPayload, oldKeyPair, TestTime);
            WebhookSignature newSig = signer.Sign(TestPayload, newKeyPair, TestTime);

            string dualHeader = $"t={TestTime.TotalSeconds},v1_es256={oldSig.Signature},v1_es256={newSig.Signature}";

            Assert.True(signer.Verify(TestPayload, dualHeader, newKeyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
            Assert.True(signer.Verify(TestPayload, dualHeader, oldKeyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }

        [Theory]
        [InlineData(299, true)]   // Within 5m tolerance
        [InlineData(301, false)]  // Expired -> Blocked
        [InlineData(-301, false)] // Future clock drift -> Blocked
        public void Verify_EnforcesClockSkewToleranceBoundaries(int secondsOffset, bool expectedResult) {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            UnixTimestamp verificationTime = TestTime + TimeSpan.FromSeconds(secondsOffset);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                verificationTime);

            Assert.Equal(expectedResult, isValid);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. EDGE-CASE PAYLOADS & MALFORMED HEADERS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheEdgeCasesAndMalformedHeaders {
        [Fact]
        public void SignAndVerify_Succeeds_WithEmptyPayload() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);
            byte[] empty = [];

            WebhookSignature signature = signer.Sign(empty, keyPair, TestTime);
            Assert.True(signer.Verify(empty, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }

        [Fact]
        public void SignAndVerify_Succeeds_WithComplexUnicodeAndEmojis() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);
            byte[] unicodePayload = Encoding.UTF8.GetBytes("{\"msg\":\"ECDSA Doğrulandı 🚀 Türkçe: ğüşıöç 🌍\"}");

            WebhookSignature signature = signer.Sign(unicodePayload, keyPair, TestTime);
            Assert.True(signer.Verify(unicodePayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }

        [Fact]
        public void Verify_HandlesCommaBombDoS_Gracefully() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            string commaBomb = $"t={TestTime.TotalSeconds}," + new string(',', 4000) + $"v1_es256={signature.Signature}," + new string(',', 4000);

            Assert.True(signer.Verify(TestPayload, commaBomb, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }
    }

    public sealed class TheNullAndBoundaryGuards {
        [Fact]
        public void Sign_Throws_WhenKeyPairIsNull() {
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            Assert.ThrowsAny<ArgumentNullException>(() =>
                signer.Sign(TestPayload, (EcdsaKeyPair)null!, TestTime));
        }

        [Fact]
        public void Sign_Throws_WhenSecretKeyIsDefault() {
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            Assert.ThrowsAny<ArgumentException>(() =>
                signer.Sign(TestPayload, default(Secret<byte>), TestTime));
        }

        [Fact]
        public void Verify_Throws_WhenPublicKeyIsNull() {
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            Assert.ThrowsAny<ArgumentNullException>(() =>
                signer.Verify(TestPayload, "t=1700000000,v1_es256=abc", (EcdsaPublicKey)null!, TimeSpan.FromMinutes(5), TestTime));
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSecretKeyIsDefault() {
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            // 🌟 DÜZELTME: Exception beklemek yerine 'false' döndüğünü doğrula
            bool isValid = signer.Verify(TestPayload, "t=1700000000,v1_es256=abc", default(Secret<byte>), TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSecretKeySpanIsEmpty() {
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            bool isValid = signer.Verify(TestPayload, "t=1700000000,v1_es256=abc", ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_Throws_WhenToleranceIsNegative() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);
            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                signer.Verify(TestPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromSeconds(-1), TestTime));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Verify_ReturnsFalse_WhenSignatureHeaderIsNullOrWhiteSpace(string? invalidHeader) {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            bool isValid = signer.Verify(TestPayload, invalidHeader!, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }
    }
}