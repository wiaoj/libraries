using System.Text;
using Wiaoj.Webhooks.Signing.Asymmetric.Rsa;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit.Rsa;

[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "RsaAttacks")]
public sealed class RsaCryptographicAttackTests {
    private static readonly byte[] CanonicalPayload = "{\"orderId\":\"ORD-888999\",\"amount\":1500.00,\"currency\":\"USD\"}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    // ────────────────────────────────────────────────────────────────────────
    // 1. MAN-IN-THE-MIDDLE & BYTE-LEVEL TAMPERING
    // ────────────────────────────────────────────────────────────────────────

    public sealed class PayloadAndSignatureTampering {
        [Fact]
        public void Verify_ReturnsFalse_WhenSingleBitIsFlippedInPayload() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // Mutate exact 1 bit in payload
            byte[] tamperedPayload = (byte[])CanonicalPayload.Clone();
            tamperedPayload[^2] ^= 0b0000_0001;

            bool isValid = signer.Verify(tamperedPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Cryptographic integrity failed: 1-bit modified payload was accepted!");
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenTrailingNullByteIsAppendedToPayload() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // Inject invisible trailing null-byte \0
            byte[] nullInjectedPayload = [.. CanonicalPayload, 0x00];

            bool isValid = signer.Verify(nullInjectedPayload, signature.HeaderValue, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Security flaw: Trailing null-byte was ignored by signature verifier!");
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenSingleCharIsMutatedInBase64Signature() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // Mutate a character in the Base64 signature
            char[] sigChars = signature.Signature.ToCharArray();
            sigChars[10] = sigChars[10] == 'A' ? 'B' : 'A';
            string tamperedHeader = $"t={BaseTimestamp.TotalSeconds},v1_ps256={new string(sigChars)}";

            bool isValid = signer.Verify(CanonicalPayload, tamperedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Security flaw: Mutated signature was accepted!");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. TRUNCATED SIGNATURES & MALFORMED BASE64
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TruncatedAndMalformedSignatures {
        [Fact]
        public void Verify_ReturnsFalse_WhenSignatureIsTruncatedBySingleByte() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // 2048-bit RSA produces exactly 256 bytes. Truncate to 255 bytes.
            byte[] rawSig = Convert.FromBase64String(signature.Signature);
            byte[] truncatedSig = rawSig[..^1];
            string truncatedHeader = $"t={BaseTimestamp.TotalSeconds},v1_ps256={Convert.ToBase64String(truncatedSig)}";

            bool isValid = signer.Verify(CanonicalPayload, truncatedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Security flaw: 255-byte truncated signature did not fail verification!");
        }

        [Theory]
        [InlineData("t=1700000000,v1_ps256=!!!!not-base64!!!!")]
        [InlineData("t=1700000000,v1_ps256=AA==")] // 1 byte
        [InlineData("t=1700000000,v1_ps256=AAAA")] // 3 bytes
        [InlineData("t=1700000000,v1_ps256=")]
        public void Verify_ReturnsFalse_ForMalformedBase64_WithoutThrowingExceptions(string malformedHeader) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            bool isValid = signer.Verify(CanonicalPayload, malformedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. REPLAY ATTACK BOUNDARIES & PARAMETER POLLUTION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ReplayAndPollutionAttacks {
        [Theory]
        [InlineData(299, true)]   // +4m 59s -> Within 5m tolerance -> Valid
        [InlineData(300, true)]   // Exactly 5m 00s -> Valid boundary
        [InlineData(301, false)]  // +5m 01s -> Expired replay attack -> Blocked
        [InlineData(-301, false)] // -5m 01s in future -> Clock skew drift -> Blocked
        public void Verify_StrictlyEnforcesClockSkewToleranceBoundaries(int secondsOffset, bool expectedResult) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);
            UnixTimestamp verificationTimestamp = BaseTimestamp + TimeSpan.FromSeconds(secondsOffset);

            bool isValid = signer.Verify(
                CanonicalPayload,
                signature.HeaderValue,
                keyPair.PublicKey,
                TimeSpan.FromMinutes(5),
                verificationTimestamp);

            Assert.Equal(expectedResult, isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenMultipleTimestampsInjected_ParameterPollution() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // Injected second t= timestamp to confuse parser
            string pollutedHeader = $"t={BaseTimestamp.TotalSeconds},v1_ps256={signature.Signature},t={BaseTimestamp.TotalSeconds + 100}";

            bool isValid = signer.Verify(CanonicalPayload, pollutedHeader, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid, "Parameter pollution vulnerability: Multiple timestamps were accepted!");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. DENIAL OF SERVICE & DELIMITER BOMBS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class DenialOfServiceDefenses {
        [Fact]
        public void Verify_Handles5000CommaBomb_WithoutMemorySpikeOrHangs() {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            WebhookSignature signature = signer.Sign(CanonicalPayload, keyPair, BaseTimestamp);

            // 5,000 commas inserted before and after signature
            string commaBomb = $"t={BaseTimestamp.TotalSeconds}," + new string(',', 5000) + $"v1_ps256={signature.Signature}," + new string(',', 5000);

            bool isValid = signer.Verify(CanonicalPayload, commaBomb, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData("-9223372036854775808")] // long.MinValue
        [InlineData("9223372036854775807")]  // long.MaxValue
        [InlineData("-1")]
        public void Verify_HandlesExtremeInt64Timestamps_WithoutOverflowCrash(string extremeTimestamp) {
            using RsaKeyPair keyPair = RsaKeyPair.Generate2048();
            RsaWebhookSigner signer = new(RsaAlgorithm.PS256);

            string header = $"t={extremeTimestamp},v1_ps256=dGVzdA==";

            bool isValid = signer.Verify(CanonicalPayload, header, keyPair.PublicKey, TimeSpan.FromMinutes(5), BaseTimestamp);

            Assert.False(isValid);
        }
    }
}