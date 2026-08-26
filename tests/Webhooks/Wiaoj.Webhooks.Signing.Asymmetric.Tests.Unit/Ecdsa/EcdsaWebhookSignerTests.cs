using System.Text;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Ecdsa;

/// <summary>
/// Unit tests for <see cref="EcdsaWebhookSigner"/> verifying ECDSA algorithms, NIST curves, zero-downtime rotation, and boundary guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "ECDSA")]
public sealed class EcdsaWebhookSignerTests {
    private static readonly byte[] TestPayload = "{\"event\":\"payment.captured\",\"amount\":42.00}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    private const int P256ExpectedSignatureByteLength = 64;
    private const int P384ExpectedSignatureByteLength = 96;
    private const int P521ExpectedSignatureByteLength = 132;

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

    public sealed class TheCurveMatrix {
        [Fact]
        public void SignAndVerify_WithNistP256_Succeeds() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            Assert.Equal(P256ExpectedSignatureByteLength, rawSig.Length);

            bool isValid = signer.Verify(TestPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_WithNistP384_Succeeds() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP384();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES384);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            Assert.Equal(P384ExpectedSignatureByteLength, rawSig.Length);

            bool isValid = signer.Verify(TestPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_WithNistP521_Succeeds() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP521();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES512);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);

            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            Assert.Equal(P521ExpectedSignatureByteLength, rawSig.Length);

            bool isValid = signer.Verify(TestPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }
    }

    public sealed class TheSecurityGuards {
        [Fact]
        public void Verify_ReturnsFalse_WhenPayloadIsTampered() {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, keyPair, TestTime);
            byte[] tampered = "{\"event\":\"payment.captured\",\"amount\":9999.00}"u8.ToArray();

            bool isValid = signer.Verify(tampered, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenVerifiedWithWrongPublicKey() {
            using EcdsaKeyPair signerKey = EcdsaKeyPair.GenerateP256();
            using EcdsaKeyPair attackerKey = EcdsaKeyPair.GenerateP256();
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            WebhookSignature signature = signer.Sign(TestPayload, signerKey, TestTime);
            bool isValid = signer.Verify(TestPayload, signature.HeaderValue, attackerKey.PublicKey, TimeSpan.FromMinutes(5), TestTime);

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
    }

    public sealed class ThePayloadEdgeCases {
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

            bool isValid = signer.Verify(TestPayload, "t=1700000000,v1_es256=abc", default(Secret<byte>), TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSecretKeySpanIsEmpty() {
            EcdsaWebhookSigner signer = new(EcdsaAlgorithm.ES256);

            bool isValid = signer.Verify(TestPayload, "t=1700000000,v1_es256=abc", ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }
    }
}