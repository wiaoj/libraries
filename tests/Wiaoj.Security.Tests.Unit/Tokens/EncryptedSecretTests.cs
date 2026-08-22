namespace Wiaoj.Security.Tests.Unit.Tokens;

[Trait("Category", "Unit")]
[Trait("Feature", "Tokens")]
public class EncryptedSecretTests {

    [Fact]
    public void ToCompactString_ShouldFormatCorrectly() {
        // Arrange
        byte[] packet = new byte[32];
        Base64UrlString base64Url = Base64UrlString.FromBytes(packet);
        CipherBlob blob = CipherBlob.From(base64Url);
        EncryptedSecret<WebhookTestContext> secret = EncryptedSecret<WebhookTestContext>.FromPersisted(blob, KeyVersion.Of(1));

        // Act
        string token = secret.ToCompactString();

        // Assert
        Assert.StartsWith("v1.", token);
        Assert.Equal($"v1.{base64Url.Value}", token);
    }

    [Fact]
    public void Parse_WithValidToken_ShouldReturnEncryptedSecret() {
        // Arrange
        byte[] packet = new byte[32];
        Base64UrlString base64Url = Base64UrlString.FromBytes(packet);
        string token = $"v3.{base64Url.Value}";

        // Act
        EncryptedSecret<WebhookTestContext> secret = EncryptedSecret<WebhookTestContext>.Parse(token);

        // Assert
        Assert.Equal(3, secret.KeyVersion.Value);
        Assert.Equal(base64Url.Value, secret.Blob.ToStorageString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid_format")]
    [InlineData("v1")]
    [InlineData("1.blob")]
    [InlineData("v-1.blob")]
    public void Parse_WithInvalidToken_ShouldThrowException(string? invalidToken) {
        if(string.IsNullOrWhiteSpace(invalidToken)) {
            Assert.ThrowsAny<ArgumentException>(() => EncryptedSecret<WebhookTestContext>.Parse(invalidToken!));
        }
        else {
            Assert.Throws<FormatException>(() => EncryptedSecret<WebhookTestContext>.Parse(invalidToken));
        }
    }

    [Fact]
    public void TryParse_WithValidToken_ShouldReturnTrueAndPopulateResult() {
        // Arrange
        byte[] packet = new byte[32];
        Base64UrlString base64Url = Base64UrlString.FromBytes(packet);
        string token = $"v2.{base64Url.Value}";

        // Act
        bool success = EncryptedSecret<WebhookTestContext>.TryParse(token, out EncryptedSecret<WebhookTestContext> secret);

        // Assert
        Assert.True(success);
        Assert.Equal(2, secret.KeyVersion.Value);
        Assert.Equal(base64Url.Value, secret.Blob.ToStorageString());
    }

    [Fact]
    public void ToString_ShouldNeverExposeRawCiphertext() {
        // Arrange
        byte[] packet = new byte[32];
        CipherBlob blob = CipherBlob.From(Base64UrlString.FromBytes(packet));
        EncryptedSecret<WebhookTestContext> secret = EncryptedSecret<WebhookTestContext>.FromPersisted(blob, KeyVersion.Of(1));

        // Act
        string display = secret.ToString();

        // Assert
        Assert.Equal("[ENCRYPTED_SECRET<WebhookTestContext> v1]", display);
    }
}