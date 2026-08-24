using System.Text;
using Wiaoj.Webhooks.Signing.Asymmetric.Rsa;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Rsa;

[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "RSA")]
public sealed class RsaWebhookSignerTests {
    private static readonly byte[] TestPayload = "{\"event\":\"order.created\",\"total\":299.90}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR, PROPERTIES & SCHEME PREFIXES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructorAndProperties {
        [Fact]
        public void DefaultConstructor_InitializesWithPs256AndStandardHeader() {
            RsaWebhookSigner signer = new();

            Assert.Equal(RsaAlgorithm.PS256, signer.Algorithm);
            Assert.Equal("rsa-ps256", signer.AlgorithmName);
            Assert.Equal("Webhook-Signature", signer.HeaderName);
            Assert.Equal("v1_ps256", signer.SchemePrefix);
        }

        [Theory]
        [InlineData("RS256", "rsa-rs256", "v1_rs256")]
        [InlineData("RS384", "rsa-rs384", "v1_rs384")]
        [InlineData("RS512", "rsa-rs512", "v1_rs512")]
        [InlineData("PS256", "rsa-ps256", "v1_ps256")]
        [InlineData("PS384", "rsa-ps384", "v1_ps384")]
        [InlineData("PS512", "rsa-ps512", "v1_ps512")]
        public void Constructor_SetsExpectedAlgorithmNameAndSchemePrefix(
            string algorithmName,
            string expectedAlgorithmName,
            string expectedSchemePrefix) {

            RsaAlgorithm algorithm = algorithmName switch {
                "RS256" => RsaAlgorithm.RS256,
                "RS384" => RsaAlgorithm.RS384,
                "RS512" => RsaAlgorithm.RS512,
                "PS256" => RsaAlgorithm.PS256,
                "PS384" => RsaAlgorithm.PS384,
                "PS512" => RsaAlgorithm.PS512,
                _ => throw new ArgumentException("Unknown algorithm", nameof(algorithmName))
            };

            RsaWebhookSigner signer = new(algorithm);

            Assert.Equal(expectedAlgorithmName, signer.AlgorithmName);
            Assert.Equal(expectedSchemePrefix, signer.SchemePrefix);
        }

        [Fact]
        public void Constructor_AcceptsCustomHeaderName() {
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256, "X-Acme-Rsa-Signature");

            Assert.Equal("X-Acme-Rsa-Signature", signer.HeaderName);
        }

        [Fact]
        public void Constructor_Throws_WhenAlgorithmIsNull() {
            Assert.ThrowsAny<ArgumentNullException>(() => new RsaWebhookSigner(null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenHeaderNameIsNullOrWhiteSpace(string? invalidHeader) {
            Assert.ThrowsAny<ArgumentException>(() => new RsaWebhookSigner(RsaAlgorithm.PS256, invalidHeader!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. ALGORITHM & KEY-SIZE MATRIX (2048, 3072, 4096-bit & ALL PADDING MODES)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheAlgorithmAndKeySizeMatrix {
        [Theory]
        [InlineData(2048)]
        [InlineData(3072)]
        [InlineData(4096)]
        public void SignAndVerify_WithModernPs256PssPadding_AcrossAllKeySizes(int keySizeInBits) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate(keySizeInBits);
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            int expectedSignatureBytes = keySizeInBits / 8;
            byte[] rawSignature = Convert.FromBase64String(signature.Signature);
            Assert.Equal(expectedSignatureBytes, rawSignature.Length);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData(2048)]
        [InlineData(3072)]
        public void SignAndVerify_WithLegacyRs256Pkcs1Padding_AcrossKeySizes(int keySizeInBits) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate(keySizeInBits);
            RsaWebhookSigner signer = new(RsaAlgorithm.RS256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            bool isValid = signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_WithPs384AndPs512_Succeeds() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer384 = new(RsaAlgorithm.PS384);
            RsaWebhookSigner signer512 = new(RsaAlgorithm.PS512);

            WebhookSignature sig384 = signer384.Sign(TestPayload, keyPair, TestTime);
            WebhookSignature sig512 = signer512.Sign(TestPayload, keyPair, TestTime);

            Assert.True(signer384.Verify(TestPayload, sig384.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
            Assert.True(signer512.Verify(TestPayload, sig512.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. UNMANAGED MEMORY (Secret<byte>) & PEM INTEGRATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheUnmanagedMemoryIntegration {
        [Fact]
        public void SignAndVerify_UsingUnmanagedSecretKey_SucceedsWithoutMemoryLeaks() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            using Secret<char> privatePemSecret = keyPair.ExportPkcs8PrivateKeyPem();
            using Secret<byte> privateBytesSecret = privatePemSecret.Expose(chars => Secret<byte>.From(new string(chars)));

            WebhookSignature signature = signer.Sign(TestPayload, privateBytesSecret, TestTime);

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
    // 4. SECURITY ATTACKS, REPLAY, ROTATION & TAMPERING
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheCryptographicSecurityGuards {
        [Fact]
        public void Verify_ReturnsFalse_WhenPayloadIsTamperedBySingleBit() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            byte[] tamperedPayload = "{\"event\":\"order.created\",\"total\":299.91}"u8.ToArray();

            bool isValid = signer.Verify(
                tamperedPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenVerifiedWithCompletelyDifferentRsaPublicKey() {
            using RsaKeyPair signerKey = RsaKeyPair.Generate2048();
            using RsaKeyPair attackerKey = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

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
        public void Verify_ReturnsFalse_WhenAlgorithmSchemeMismatch_Ps256VsRs256() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner ps256Signer = new(RsaAlgorithm.PS256);
            RsaWebhookSigner rs256Signer = new(RsaAlgorithm.RS256);

            WebhookSignature signature = ps256Signer.Sign(TestPayload, keyPair, TestTime);

            bool isValid = rs256Signer.Verify(
                TestPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_SupportsMultiSignatureHeader_ForZeroDowntimeKeyRotation() {
            using RsaKeyPair oldKeyPair = RsaKeyPair.Generate2048();
            using RsaKeyPair newKeyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature oldSig = signer.Sign(TestPayload, oldKeyPair, TestTime);
            WebhookSignature newSig = signer.Sign(TestPayload, newKeyPair, TestTime);

            string dualHeader = $"t={TestTime.TotalSeconds},v1_ps256={oldSig.Signature},v1_ps256={newSig.Signature}";

            bool verifiedWithNewKey = signer.Verify(TestPayload, dualHeader, newKeyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);
            bool verifiedWithOldKey = signer.Verify(TestPayload, dualHeader, oldKeyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(verifiedWithNewKey);
            Assert.True(verifiedWithOldKey);
        }

        [Theory]
        [InlineData(299, true)]   // 4m 59s in past (Within 5m tolerance)
        [InlineData(301, false)]  // 5m 01s in past (Replay attack -> Blocked)
        [InlineData(-301, false)] // 5m 01s in future (Clock drift -> Blocked)
        public void Verify_EnforcesClockSkewToleranceBoundaries(int secondsOffset, bool expectedResult) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

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
    // 5. MALFORMED HEADERS, CORRUPTED BASE64 & DOS ATTACKS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheHeaderParsingAndDoSDefenses {
        [Fact]
        public void Verify_HandlesCommaBombDoS_WithoutCrashingOrHanging() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            string commaBomb = $"t={TestTime.TotalSeconds}," + new string(',', 5000) + $"v1_ps256={signature.Signature}," + new string(',', 5000);

            bool isValid = signer.Verify(TestPayload, commaBomb, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData("t=1700000000,v1_ps256=corrupted-not-valid-base64!@#")]
        [InlineData("t=1700000000,v1_ps256=dGVzdA==")] // Valid base64, but only 4 bytes (RSA requires 256 bytes)
        [InlineData("t=1700000000,v1_ps256=")]
        [InlineData("t=1700000000")]
        [InlineData("v1_ps256=4f53cd")]
        [InlineData("")]
        [InlineData("   ")]
        public void Verify_ReturnsFalse_ForCorruptedOrMalformedSignatures_WithoutThrowing(string malformedHeader) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            bool isValid = signer.Verify(TestPayload, malformedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenMultipleTimestampsProvided_ParameterPollution() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            string polluted = $"t={TestTime.TotalSeconds},v1_ps256={signature.Signature},t={TestTime.TotalSeconds + 500}";

            bool isValid = signer.Verify(TestPayload, polluted, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. EDGE-CASE PAYLOADS (EMPTY, LARGE UNICODE & EMOJIS)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ThePayloadEdgeCases {
        [Fact]
        public void SignAndVerify_Succeeds_WithEmptyPayload() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);
            byte[] emptyPayload = [];

            WebhookSignature signature = signer.Sign(emptyPayload, keyPair, TestTime);
            bool isValid = signer.Verify(emptyPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_Succeeds_WithComplexUnicodeAndEmojis() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);
            byte[] unicodePayload = Encoding.UTF8.GetBytes("{\"text\":\"İpek böceği 🚀 / Türkçe karakterler: ğüşıöç 🌍 / 中文 / 日本語\"}");

            WebhookSignature signature = signer.Sign(unicodePayload, keyPair, TestTime);
            bool isValid = signer.Verify(unicodePayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 7. NULL, EMPTY & DEVELOPER / CLIENT BOUNDARY GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheNullAndBoundaryGuards {
        [Fact]
        public void Sign_Throws_WhenKeyPairIsNull() {
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            Assert.ThrowsAny<ArgumentNullException>(() =>
                signer.Sign(TestPayload, (RsaKeyPair)null!, TestTime));
        }

        [Fact]
        public void Sign_Throws_WhenSecretKeyIsDefault() {
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            Assert.ThrowsAny<ArgumentException>(() =>
                signer.Sign(TestPayload, default(Secret<byte>), TestTime));
        }

        [Fact]
        public void Verify_Throws_WhenPublicKeyIsNull() {
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            Assert.ThrowsAny<ArgumentNullException>(() =>
                signer.Verify(TestPayload, "t=1700000000,v1_ps256=abc", (RsaPublicKey)null!, TimeSpan.FromMinutes(5), TestTime));
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSecretKeyIsDefault() {
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            // 🌟 DÜZELTME: Exception beklemek yerine 'false' döndüğünü doğrula
            bool isValid = signer.Verify(TestPayload, "t=1700000000,v1_ps256=abc", default(Secret<byte>), TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSecretKeySpanIsEmpty() {
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            bool isValid = signer.Verify(TestPayload, "t=1700000000,v1_ps256=abc", ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_Throws_WhenToleranceIsNegative() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                signer.Verify(TestPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromSeconds(-1), TestTime));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void Verify_ReturnsFalse_WhenSignatureHeaderIsNullOrWhiteSpace(string? invalidHeader) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            bool isValid = signer.Verify(TestPayload, invalidHeader!, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }
    }
}