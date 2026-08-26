using Wiaoj.Webhooks.Signing;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

/// <summary>
/// Unit tests for <see cref="HmacSha512WebhookSigner"/> verifying HMAC-SHA512 algorithm properties and end-to-end signing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Webhooks")]
[Trait("Feature", "HmacSha512")]
public sealed class HmacSha512WebhookSignerTests {
    private readonly HmacSha512WebhookSigner _signer = new();
    private static readonly byte[] TestKey = "whsec_test_sha512_secret_key_long_enough_for_security"u8.ToArray();
    private static readonly byte[] TestPayload = "{\"event\":\"payment.completed\",\"id\":\"pay_999\"}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    private const int ExpectedHexSignatureLength = 128;

    [Fact]
    public void Properties_ReturnExpectedDefaults() {
        Assert.Equal("hmac-sha512", this._signer.AlgorithmName);
        Assert.Equal("Webhook-Signature", this._signer.HeaderName);
        Assert.Equal("v2", this._signer.SchemePrefix);
    }

    [Fact]
    public void Constructor_AcceptsCustomHeaderName() {
        HmacSha512WebhookSigner customSigner = new("Custom-Sha512-Header");
        Assert.Equal("Custom-Sha512-Header", customSigner.HeaderName);
    }

    [Fact]
    public void Sign_ProducesDeterministic128CharHexSignature() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);

        Assert.Equal(ExpectedHexSignatureLength, signature.Signature.Length);
        Assert.StartsWith("t=1700000000,v2=", signature.HeaderValue);
    }

    [Fact]
    public void Sign_WithSecretKey_ProducesSameSignatureAsRawBytes() {
        using Secret<byte> secret = Secret<byte>.From(TestKey);

        WebhookSignature rawSig = this._signer.Sign(TestPayload, TestKey, TestTime);
        WebhookSignature secretSig = this._signer.Sign(TestPayload, secret, TestTime);

        Assert.Equal(rawSig, secretSig);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForValidHmacSha512Signature() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenSha256HeaderPassedToSha512Signer() {
        const string v1Header = "t=1700000000,v1=4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";
        bool isValid = this._signer.Verify(TestPayload, v1Header, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTamperedPayload() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        byte[] tampered = "{\"event\":\"payment.completed\",\"id\":\"pay_888\"}"u8.ToArray();

        bool isValid = this._signer.Verify(tampered, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenSecretKeyIsDifferent() {
        byte[] wrongKey = "whsec_completely_wrong_sha512_secret_key"u8.ToArray();
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);

        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, wrongKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }
}