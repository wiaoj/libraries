using Wiaoj.Webhooks.Signing;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

/// <summary>
/// Unit tests for <see cref="HmacSha256WebhookSigner"/> verifying HMAC-SHA256 algorithm properties and end-to-end signing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Webhooks")]
[Trait("Feature", "HmacSha256")]
public sealed class HmacSha256WebhookSignerTests {
    private readonly HmacSha256WebhookSigner _signer = new();
    private static readonly byte[] TestKey = "whsec_test_secret_key_1234567890"u8.ToArray();
    private static readonly byte[] TestPayload = "{\"event\":\"order.created\",\"amount\":99.95}"u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

    private const int ExpectedHexSignatureLength = 64;

    [Fact]
    public void Properties_ReturnExpectedDefaults() {
        Assert.Equal("hmac-sha256", this._signer.AlgorithmName);
        Assert.Equal("Webhook-Signature", this._signer.HeaderName);
        Assert.Equal("v1", this._signer.SchemePrefix);
    }

    [Fact]
    public void Constructor_AcceptsCustomHeaderName() {
        HmacSha256WebhookSigner customSigner = new("Custom-Webhook-Signature");
        Assert.Equal("Custom-Webhook-Signature", customSigner.HeaderName);
    }

    [Fact]
    public void Sign_ProducesDeterministic64CharHexSignature() {
        WebhookSignature signature1 = this._signer.Sign(TestPayload, TestKey, TestTime);
        WebhookSignature signature2 = this._signer.Sign(TestPayload, TestKey, TestTime);

        Assert.Equal(signature1, signature2);
        Assert.StartsWith("t=1700000000,v1=", signature1.HeaderValue);
        Assert.Equal(ExpectedHexSignatureLength, signature1.Signature.Length);
    }

    [Fact]
    public void Sign_WithSecretKey_ProducesSameSignatureAsRawBytes() {
        using Secret<byte> secret = Secret<byte>.From(TestKey);

        WebhookSignature rawSig = this._signer.Sign(TestPayload, TestKey, TestTime);
        WebhookSignature secretSig = this._signer.Sign(TestPayload, secret, TestTime);

        Assert.Equal(rawSig, secretSig);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForAuthenticHmacSha256Signature() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_WithSecretKey_ReturnsTrue_ForAuthenticSignature() {
        using Secret<byte> secret = Secret<byte>.From(TestKey);
        WebhookSignature signature = this._signer.Sign(TestPayload, secret, TestTime);

        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, secret, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenSecretKeyIsDifferent() {
        byte[] wrongKey = "whsec_completely_wrong_secret_key"u8.ToArray();
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);

        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, wrongKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenPayloadIsTampered() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        byte[] tamperedPayload = "{\"event\":\"order.created\",\"amount\":99.96}"u8.ToArray();

        bool isValid = this._signer.Verify(tamperedPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTimestampIsTamperedInHeader() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        string tamperedHeader = $"t={TestTime.TotalSeconds + 10},v1={signature.Signature}";

        bool isValid = this._signer.Verify(TestPayload, tamperedHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }
}