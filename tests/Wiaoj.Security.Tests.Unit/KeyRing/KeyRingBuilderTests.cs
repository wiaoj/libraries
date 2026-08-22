using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Security.Tests.Unit.KeyRing;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyRing")]
public class KeyRingBuilderTests {

    private static EncryptionKey CreateKey(int version, bool isRetired = false) {
        return new EncryptionKey(KeyVersion.Of(version), AesGcmKey.Generate256(), isRetired);
    }

    [Fact]
    public void Build_WithoutCurrentKey_ShouldThrowInvalidOperationException() {
        // Arrange
        KeyRingBuilder<WebhookTestContext> builder = new();
        using EncryptionKey retiredKey = CreateKey(1, isRetired: true);
        builder.WithRetiredKey(retiredKey);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void WithCurrentKey_WhenCalledTwice_ShouldThrowInvalidOperationException() {
        // Arrange
        KeyRingBuilder<WebhookTestContext> builder = new();
        using EncryptionKey k1 = CreateKey(1);
        using EncryptionKey k2 = CreateKey(2);

        builder.WithCurrentKey(k1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.WithCurrentKey(k2));
    }

    [Fact]
    public void WithRetiredKey_WithDuplicateVersion_ShouldThrowArgumentException() {
        // Arrange
        KeyRingBuilder<WebhookTestContext> builder = new();
        using EncryptionKey k1 = CreateKey(1, isRetired: false);
        using EncryptionKey k1Duplicate = CreateKey(1, isRetired: true);

        builder.WithCurrentKey(k1);

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => builder.WithRetiredKey(k1Duplicate));
    }

    [Fact]
    public void BuildProtector_ShouldReturnFunctionalSecretProtector() {
        // Arrange
        using EncryptionKey activeKey = CreateKey(1);
        using SecretProtector<WebhookTestContext> protector = new KeyRingBuilder<WebhookTestContext>()
            .WithCurrentKey(activeKey)
            .BuildProtector();

        // Act
        EncryptedSecret<WebhookTestContext> secret = protector.Protect("test-secret");

        // Assert
        Assert.Equal(1, protector.CurrentKeyVersion.Value);
        Assert.Equal(1, secret.KeyVersion.Value);
    }
}