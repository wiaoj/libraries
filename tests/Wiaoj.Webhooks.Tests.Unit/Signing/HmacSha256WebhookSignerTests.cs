using System.Text;
using Wiaoj.Webhooks.Signing;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

public sealed class HmacSha256WebhookSignerTests {
    private readonly HmacSha256WebhookSigner _signer = new();
    private static readonly byte[] TestKey = "whsec_test_secret_key_1234567890"u8.ToArray();
    private static readonly byte[] TestPayload = """{"event":"order.created","amount":99.95}"""u8.ToArray();
    private static readonly UnixTimestamp TestTime = UnixTimestamp.FromSeconds(1700000000);

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
    public void Sign_ProducesDeterministicValidSignature() {
        WebhookSignature signature1 = this._signer.Sign(TestPayload, TestKey, TestTime);
        WebhookSignature signature2 = this._signer.Sign(TestPayload, TestKey, TestTime);

        Assert.Equal(signature1, signature2);
        Assert.StartsWith("t=1700000000,v1=", signature1.HeaderValue);
        Assert.Equal(64, signature1.Signature.Length); // SHA-256 is 32 bytes = 64 hex chars
    }

    [Fact]
    public void Sign_WithSecretKey_ProducesSameSignatureAsRawBytes() {
        using Secret<byte> secret = Secret<byte>.From(TestKey);

        WebhookSignature rawSig = this._signer.Sign(TestPayload, TestKey, TestTime);
        WebhookSignature secretSig = this._signer.Sign(TestPayload, secret, TestTime);

        Assert.Equal(rawSig, secretSig);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForAuthenticSignatureWithinTolerance() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        TimeSpan tolerance = TimeSpan.FromMinutes(5);

        // Verification done 1 minute later
        UnixTimestamp verifyTime = TestTime + TimeSpan.FromMinutes(1);
        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, tolerance, verifyTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_WithSecretKey_ReturnsTrue_ForAuthenticSignature() {
        using Secret<byte> secret = Secret<byte>.From(TestKey);
        WebhookSignature signature = this._signer.Sign(TestPayload, secret, TestTime);
        TimeSpan tolerance = TimeSpan.FromMinutes(5);

        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, secret, tolerance, TestTime);

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
        byte[] tamperedPayload = """{"event":"order.created","amount":99.96}"""u8.ToArray();

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

    [Fact]
    public void Verify_ReturnsFalse_WhenTimestampIsTooOld_ClockSkewExceeded() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        TimeSpan tolerance = TimeSpan.FromMinutes(5);

        // Verification attempted 6 minutes after signature creation
        UnixTimestamp lateTime = TestTime + TimeSpan.FromMinutes(6);
        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, tolerance, lateTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTimestampIsInFuture_BeyondTolerance() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        TimeSpan tolerance = TimeSpan.FromMinutes(5);

        // Verification attempted with clock 6 minutes behind the signature
        UnixTimestamp earlyTime = TestTime - TimeSpan.FromMinutes(6);
        bool isValid = this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, tolerance, earlyTime);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid_header_without_tags")]
    [InlineData("t=invalid_timestamp,v1=4f53cda1")]
    [InlineData("t=1700000000")]
    [InlineData("v1=4f53cda1")]
    [InlineData("t=1700000000,v2=wrong_scheme_for_sha256")]
    public void Verify_ReturnsFalse_WhenHeaderIsMalformed(string malformedHeader) {
        bool isValid = this._signer.Verify(TestPayload, malformedHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsTrue_WhenHeaderContainsMultipleSignatures_AndOneMatches() {
        WebhookSignature validSig = this._signer.Sign(TestPayload, TestKey, TestTime);
        const string oldSig = "0000000000000000000000000000000000000000000000000000000000000000";
        string multiSigHeader = $"t={TestTime.TotalSeconds},v1={oldSig},v1={validSig.Signature}";

        bool isValid = this._signer.Verify(TestPayload, multiSigHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void SignAndVerify_WorksWithEmptyPayload() {
        byte[] emptyPayload = [];
        WebhookSignature signature = this._signer.Sign(emptyPayload, TestKey, TestTime);

        bool isValid = this._signer.Verify(emptyPayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void SignAndVerify_WorksWithUtf8MultiByteCharacters() {
        byte[] unicodePayload = Encoding.UTF8.GetBytes("{\"message\":\"Merhaba dünya 🌍 / 🚀 Türkçe karakterler: ğüşıöç\"}");
        WebhookSignature signature = this._signer.Sign(unicodePayload, TestKey, TestTime);

        bool isValid = this._signer.Verify(unicodePayload, signature.HeaderValue, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsTrue_WhenHeaderSignatureIsUppercase() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);
        string uppercaseHeader = $"t={TestTime.TotalSeconds},v1={signature.Signature.ToUpperInvariant()}";

        bool isValid = this._signer.Verify(TestPayload, uppercaseHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_HandlesCommaBombDoS_GracefullyWithoutFailureOrAllocationExplosion() {
        WebhookSignature validSig = this._signer.Sign(TestPayload, TestKey, TestTime);
        // Header with 5,000 extra commas simulating a DoS attempt
        string commaBomb = $"t={TestTime.TotalSeconds}," + new string(',', 5000) + $"v1={validSig.Signature}," + new string(',', 5000);

        bool isValid = this._signer.Verify(TestPayload, commaBomb, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("-9223372036854775808")] // long.MinValue
    [InlineData("9223372036854775807")]  // long.MaxValue
    [InlineData("-1")]
    [InlineData("-999999999999")]
    public void Verify_HandlesExtremeInt64Timestamps_WithoutOverflowException(string extremeTime) {
        string header = $"t={extremeTime},v1=4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";

        bool isValid = this._signer.Verify(TestPayload, header, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenMultipleTimestampsProvided_ParameterPollution() {
        WebhookSignature validSig = this._signer.Sign(TestPayload, TestKey, TestTime);
        string pollutedHeader = $"t={TestTime.TotalSeconds},v1={validSig.Signature},t={TestTime.TotalSeconds + 100}";

        bool isValid = this._signer.Verify(TestPayload, pollutedHeader, TestKey, TimeSpan.FromMinutes(5), TestTime);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_Throws_WhenToleranceIsNegative() {
        WebhookSignature signature = this._signer.Sign(TestPayload, TestKey, TestTime);

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            this._signer.Verify(TestPayload, signature.HeaderValue, TestKey, TimeSpan.FromSeconds(-1), TestTime));
    }

    [Fact]
    public void Sign_Throws_WhenSecretKeyIsEmpty() {
        Assert.ThrowsAny<ArgumentException>(() =>
            this._signer.Sign(TestPayload, [], TestTime));
    }
}
