using Wiaoj.Webhooks.Signing;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

public sealed class HmacSha512WebhookSignerTests {
    private readonly HmacSha512WebhookSigner _signer = new();
    private static readonly byte[] TestKey = "whsec_test_sha512_secret_key_long_enough_for_security"u8.ToArray();
    private static readonly byte[] TestPayload = """{"event":"payment.completed","id":"pay_999"}"""u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    [Fact]
    public void Properties_ReturnExpectedDefaults() {
        Assert.Equal("hmac-sha512", this._signer.AlgorithmName);
        Assert.Equal("Webhook-Signature", this._signer.HeaderName);
        Assert.Equal("v2", this._signer.SchemePrefix);
    }

    [Fact]
    public void Sign_Produces64ByteHexSignature() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);

        Assert.Equal(128, signature.Signature.Length); // SHA-512 is 64 bytes = 128 hex chars
        Assert.StartsWith("t=1700000000,v2=", signature.HeaderValue);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForValidHmacSha512Signature() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenSha256HeaderPassedToSha512Signer() {
        // v1 header should not match v2 signer
        const string v1Header = "t=1700000000,v1=4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";
        bool isValid = this._signer.Verify(TestPayload, v1Header, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTamperedPayload() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        byte[] tampered = """{"event":"payment.completed","id":"pay_888"}"""u8.ToArray();

        bool isValid = this._signer.Verify(tampered, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }
}
