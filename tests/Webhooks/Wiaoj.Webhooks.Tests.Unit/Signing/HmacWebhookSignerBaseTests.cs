using System.Text;
using Wiaoj.Webhooks.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

/// <summary>
/// Unit tests for <see cref="HmacWebhookSignerBase"/> focusing on constant-time verification,
/// ASCII hex constraints, multi-byte resilience, and payload edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Webhooks")]
[Trait("Feature", "HmacSigningBase")]
public sealed class HmacWebhookSignerBaseTests {
    private static readonly byte[] TestKey = "test-hmac-key-1234567890"u8.ToArray();
    private static readonly byte[] TestPayload = "{\"event\":\"hmac.test\"}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    public sealed class TheUnmanagedMemoryAndSigningIntegration {
        [Fact]
        public void SignAndVerify_WithSecretKey_ProducesMatchingAuthenticSignature() {
            FakeHmacWebhookSigner signer = new();
            using Secret<byte> secretKey = Secret<byte>.From(TestKey);

            WebhookSignature signature = signer.Sign(TestPayload, secretKey, TestTime);
            bool isValid = signer.Verify(TestPayload, signature.HeaderValue, secretKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void Sign_Throws_WhenSecretKeyIsEmpty() {
            FakeHmacWebhookSigner signer = new();
            Assert.ThrowsAny<ArgumentException>(() => signer.Sign(TestPayload, [], TestTime));
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSecretKeyIsEmpty() {
            FakeHmacWebhookSigner signer = new();
            string header = $"t={TestTime.TotalSeconds},v1=dummy";

            bool isValid = signer.Verify(TestPayload, header, ReadOnlySpan<byte>.Empty, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }
    }

    public sealed class TheConstantTimeAndEncodingDefenses {
        [Fact]
        public void Verify_ReturnsFalseWithoutThrowing_WhenCandidateContainsSingleMultiByteUnicodeChar() {
            FakeHmacWebhookSigner signer = new(computeHash: (_, _) => new string('a', 64));
            WebhookSignature sig = signer.Sign(TestPayload, TestKey, TestTime);

            string malicious = new string('a', 63) + "ç";
            string header = $"t={TestTime.TotalSeconds},v1={malicious}";

            bool isValid = signer.Verify(TestPayload, header, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalseWithoutThrowing_WhenCandidateIsEntirelyMultiByteUnicode() {
            FakeHmacWebhookSigner signer = new(computeHash: (_, _) => new string('a', 64));
            string malicious = new('ğ', 64);
            string header = $"t={TestTime.TotalSeconds},v1={malicious}";

            bool isValid = signer.Verify(TestPayload, header, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.False(isValid);
        }

        [Fact]
        public void Verify_FindsValidSignature_WhenPrecededByMalformedUnicodeCandidate() {
            FakeHmacWebhookSigner signer = new();
            WebhookSignature validSig = signer.Sign(TestPayload, TestKey, TestTime);
            string malicious = new('ö', validSig.Signature.Length);
            string header = $"t={TestTime.TotalSeconds},v1={malicious},v1={validSig.Signature}";

            bool isValid = signer.Verify(TestPayload, header, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void Verify_ReturnsTrue_WhenHeaderSignatureIsUppercaseHex() {
            FakeHmacWebhookSigner signer = new();
            WebhookSignature sig = signer.Sign(TestPayload, TestKey, TestTime);
            string uppercaseHeader = $"t={TestTime.TotalSeconds},v1={sig.Signature.ToUpperInvariant()}";

            bool isValid = signer.Verify(TestPayload, uppercaseHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }
    }

    public sealed class TheAlgorithmAndOutputLengthResilience {
        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(200)]
        public void SignAndVerify_WorkCorrectly_RegardlessOfHashOutputLength(int hashLength) {
            string fixedHash = new('f', hashLength);
            FakeHmacWebhookSigner signer = new(computeHash: (_, _) => fixedHash);

            WebhookSignature sig = signer.Sign(TestPayload, TestKey, TestTime);
            Assert.Equal(hashLength, sig.Signature.Length);

            bool isValid = signer.Verify(TestPayload, sig.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void Verify_UsesCorrectSchemePrefix_IgnoringOtherSchemesInHeader() {
            FakeHmacWebhookSigner signer = new(schemePrefix: "custom");
            WebhookSignature sig = signer.Sign(TestPayload, TestKey, TestTime);
            string header = $"t={TestTime.TotalSeconds},v1=not_relevant,{sig.HeaderValue[(sig.HeaderValue.IndexOf(',') + 1)..]}";

            bool isValid = signer.Verify(TestPayload, header, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }
    }

    public sealed class ThePayloadEdgeCasesAndKeyRotation {
        [Fact]
        public void SignAndVerify_WorksWithEmptyPayload() {
            FakeHmacWebhookSigner signer = new();
            byte[] emptyPayload = [];

            WebhookSignature signature = signer.Sign(emptyPayload, TestKey, TestTime);
            bool isValid = signer.Verify(emptyPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void SignAndVerify_WorksWithUtf8MultiByteCharacters() {
            FakeHmacWebhookSigner signer = new();
            byte[] unicodePayload = Encoding.UTF8.GetBytes("{\"msg\":\"Merhaba dünya 🚀 Türkçe: ğüşıöç 🌍\"}");

            WebhookSignature signature = signer.Sign(unicodePayload, TestKey, TestTime);
            bool isValid = signer.Verify(unicodePayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }

        [Fact]
        public void Verify_ReturnsTrue_WhenHeaderContainsMultipleSignatures_AndOneMatches() {
            FakeHmacWebhookSigner signer = new();
            WebhookSignature validSig = signer.Sign(TestPayload, TestKey, TestTime);
            const string oldSig = "0000000000000000000000000000000000000000000000000000000000000000";
            string multiSigHeader = $"t={TestTime.TotalSeconds},v1={oldSig},v1={validSig.Signature}";

            bool isValid = signer.Verify(TestPayload, multiSigHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

            Assert.True(isValid);
        }
    }
}