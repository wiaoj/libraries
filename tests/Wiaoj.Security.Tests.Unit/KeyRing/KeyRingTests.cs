using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Security.Tests.Unit.KeyRing;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyRing")]
public class KeyRingTests {

    private static EncryptionKey CreateKey(int version, bool isRetired) {
        return new EncryptionKey(KeyVersion.Of(version), AesGcmKey.Generate256(), isRetired);
    }

    [Fact]
    public void CurrentKey_ShouldReturnActiveKey() {
        // Arrange
        EncryptionKey v1 = CreateKey(1, isRetired: true);
        EncryptionKey v2 = CreateKey(2, isRetired: false);

        using KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(v1)
            .WithCurrentKey(v2)
            .Build();

        // Act & Assert
        Assert.Equal(2, ring.Count);
        Assert.Equal(2, ring.CurrentVersion.Value);
        Assert.Same(v2, ring.CurrentKey);
    }

    [Fact]
    public void GetKey_WithValidVersions_ShouldReturnCorrectKeys() {
        // Arrange
        EncryptionKey v1 = CreateKey(1, isRetired: true);
        EncryptionKey v2 = CreateKey(2, isRetired: false);

        using KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(v1)
            .WithCurrentKey(v2)
            .Build();

        // Act & Assert
        Assert.Same(v1, ring.GetKey(KeyVersion.Of(1)));
        Assert.Same(v2, ring.GetKey(KeyVersion.Of(2)));
    }

    [Fact]
    public void GetKey_WithUnknownVersion_ShouldThrowKeyNotFoundException() {
        // Arrange
        EncryptionKey v1 = CreateKey(1, isRetired: false);

        using KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithCurrentKey(v1)
            .Build();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => ring.GetKey(KeyVersion.Of(99)));
    }

    [Fact]
    public void NeedsRotation_ShouldReturnTrueForOlderVersionsOnly() {
        // Arrange
        EncryptionKey v1 = CreateKey(1, isRetired: true);
        EncryptionKey v2 = CreateKey(2, isRetired: false);

        using KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(v1)
            .WithCurrentKey(v2)
            .Build();

        // Act & Assert
        Assert.True(ring.NeedsRotation(KeyVersion.Of(1)));
        Assert.False(ring.NeedsRotation(KeyVersion.Of(2)));
    }

    [Fact]
    public void Dispose_ShouldDisposeAllContainedKeys() {
        // Arrange
        EncryptionKey v1 = CreateKey(1, isRetired: true);
        EncryptionKey v2 = CreateKey(2, isRetired: false);

        KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(v1)
            .WithCurrentKey(v2)
            .Build();

        // Act
        ring.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => ring.CurrentKey);
        Assert.Throws<ObjectDisposedException>(() => ring.GetKey(KeyVersion.Of(1)));
    }
}