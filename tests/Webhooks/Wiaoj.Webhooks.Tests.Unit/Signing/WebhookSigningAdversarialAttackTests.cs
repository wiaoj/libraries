using System.Text;
using Wiaoj.Webhooks.Signing;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "AdversarialAttacks")]
public sealed class WebhookSigningAdversarialAttackTests {
    private static readonly byte[] TestKey = "whsec_super_secret_signing_key_for_testing_12345"u8.ToArray();
    private static readonly byte[] CanonicalPayload = "{\"event\":\"order.completed\",\"amount\":99.95,\"customer\":\"John Doe\"}"u8.ToArray();
    private static readonly UnixTimestamp BaseTimestamp = UnixTimestamp.FromSeconds(1700000000);

    public sealed class TheBitFlippingAndPayloadCorruption {
        [Fact]
        public void Verify_ReturnsFalse_WhenEverySingleByteInPayloadIsFlippedOneByOne() {
            // Arrange: Generate authentic signature for the base canonical payload
            HmacSha256WebhookSigner signer = new();
            WebhookSignature signature = signer.Sign(CanonicalPayload, TestKey, BaseTimestamp);

            // Act & Assert: Mutate each byte individually to confirm total cryptographic tamper detection
            for(int i = 0; i < CanonicalPayload.Length; i++) {
                byte[] corruptedPayload = (byte[])CanonicalPayload.Clone();
                corruptedPayload[i] ^= 0xFF;

                bool isValid = signer.Verify(
                    corruptedPayload,
                    signature.HeaderValue,
                    TestKey,
                    TimeSpan.FromMinutes(5),
                    BaseTimestamp);

                Assert.False(isValid);
            }
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenNullByteInjectedIntoPayloadMiddleOrEnd() {
            // Arrange: Generate valid signature
            HmacSha256WebhookSigner signer = new();
            WebhookSignature signature = signer.Sign(CanonicalPayload, TestKey, BaseTimestamp);

            // Act: Inject invisible null bytes into middle and trailing boundaries
            byte[] middleNullPayload = [.. CanonicalPayload[..10], 0x00, .. CanonicalPayload[10..]];
            byte[] trailingNullPayload = [.. CanonicalPayload, 0x00];

            // Assert: Cryptographic verification must reject altered byte arrays
            Assert.False(signer.Verify(middleNullPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp));
            Assert.False(signer.Verify(trailingNullPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp));
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenNullByteInjectedAtPayloadStart() {
            // Arrange: Generate valid signature
            HmacSha256WebhookSigner signer = new();
            WebhookSignature signature = signer.Sign(CanonicalPayload, TestKey, BaseTimestamp);

            // Act: Inject invisible null byte at the leading boundary
            byte[] leadingNullPayload = [0x00, .. CanonicalPayload];

            // Assert: Cryptographic verification must reject altered byte arrays
            Assert.False(signer.Verify(leadingNullPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp));
        }
    }

    public sealed class TheHeaderInjectionAndSchemeCollisions {
        [Fact]
        public void Verify_RejectsSignature_WhenAttackerInjectsForgedEarlierTimestamp() {
            // Arrange: Generate valid signature for canonical timestamp
            HmacSha256WebhookSigner signer = new();
            WebhookSignature genuineSignature = signer.Sign(CanonicalPayload, TestKey, BaseTimestamp);

            // Act: Attacker modifies the header timestamp to 10 minutes prior while keeping original signature hash
            string forgedHeader = $"t={BaseTimestamp.TotalSeconds - 600},v1={genuineSignature.Signature}";

            // Assert: Verification must fail because timestamp is cryptographically bound into canonical bytes
            bool isValid = signer.Verify(CanonicalPayload, forgedHeader, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp);
            Assert.False(isValid);
        }

        [Fact]
        public void Verify_HandlesHeaderWithMultipleMixedSchemes_AndOnlyMatchesExactScheme() {
            // Arrange: Create signers and valid signatures for both SHA256 and SHA512
            HmacSha256WebhookSigner sha256Signer = new();
            HmacSha512WebhookSigner sha512Signer = new();

            WebhookSignature sha256Sig = sha256Signer.Sign(CanonicalPayload, TestKey, BaseTimestamp);
            WebhookSignature sha512Sig = sha512Signer.Sign(CanonicalPayload, TestKey, BaseTimestamp);

            // Construct multi-scheme header containing v1, v2, and forged signatures
            string complexHeader = $"t={BaseTimestamp.TotalSeconds},v2={sha512Sig.Signature},v1_es256=forged_base64_sig,v1={sha256Sig.Signature}";

            // Assert: Each signer must selectively extract and verify its own scheme prefix without interference
            Assert.True(sha256Signer.Verify(CanonicalPayload, complexHeader, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp));
            Assert.True(sha512Signer.Verify(CanonicalPayload, complexHeader, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp));
        }

        [Theory]
        [InlineData("t=1700000000\r\nv1=abc")]
        [InlineData("t=1700000000\0v1=abc")]
        [InlineData("t=1700000000\t,\tv1=abc")]
        public void Verify_RejectsControlCharactersAndCRLFInjectionInHeaders(string maliciousHeader) {
            // Act & Assert: Header parser must reject control characters and CRLF injection attempts without throwing
            HmacSha256WebhookSigner signer = new();

            bool isValid = signer.Verify(CanonicalPayload, maliciousHeader, TestKey, TimeSpan.FromMinutes(5), BaseTimestamp);
            Assert.False(isValid);
        }
    }
}