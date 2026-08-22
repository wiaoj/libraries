using Microsoft.Extensions.Options;
using Wiaoj.Security.Testing;

namespace Wiaoj.Security.Tests.Unit.KeyRing;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyRing")]
public class KeyRingLoaderTests {

    private static (KeyRingLoader<WebhookTestContext> Loader, InMemoryEncryptionKeyStore Store) CreateLoader(int keySizeInBits = 256) {
        InMemoryEncryptionKeyStore store = new();
        FakeMasterKeyProvider masterKeyProvider = new();
        IOptions<KeyRotationOptions> options = Options.Create(new KeyRotationOptions { KeySizeInBits = keySizeInBits });

        KeyRingLoader<WebhookTestContext> loader = new(
            store,
            masterKeyProvider,
            options,
            TimeProvider.System);

        return (loader, store);
    }

    [Fact]
    public async Task LoadAsync_WhenNoKeysExist_ShouldBootstrapVersion1() {
        // Arrange
        (KeyRingLoader<WebhookTestContext>? loader, InMemoryEncryptionKeyStore? store) = CreateLoader();

        // Act
        using KeyRing<WebhookTestContext> ring = await loader.LoadAsync();

        // Assert
        Assert.Equal(1, ring.Count);
        Assert.Equal(1, ring.CurrentVersion.Value);

        IReadOnlyList<EncryptionKeyRecord> storedKeys = await store.LoadKeysAsync(nameof(WebhookTestContext));
        Assert.Single(storedKeys);
        Assert.Equal(1, storedKeys[0].Version);
        Assert.False(storedKeys[0].IsRetired);
    }

    [Fact]
    public async Task LoadAsync_WhenKeysAlreadyExist_ShouldLoadExistingRing() {
        // Arrange
        (KeyRingLoader<WebhookTestContext>? loader, InMemoryEncryptionKeyStore _) = CreateLoader();

        // İlk seferde v1 oluşturulur
        using(KeyRing<WebhookTestContext> initialRing = await loader.LoadAsync()) {
            Assert.Equal(1, initialRing.CurrentVersion.Value);
        }

        // Act (İkinci çağrı var olanı okumalı)
        using KeyRing<WebhookTestContext> reloadedRing = await loader.LoadAsync();

        // Assert
        Assert.Equal(1, reloadedRing.Count);
        Assert.Equal(1, reloadedRing.CurrentVersion.Value);
    }

    [Fact]
    public async Task LoadAsync_WhenAllKeysAreRetired_ShouldThrowInvalidOperationException() {
        // Arrange
        (KeyRingLoader<WebhookTestContext>? loader, InMemoryEncryptionKeyStore? store) = CreateLoader();

        // v1 bootstrap et
        using(await loader.LoadAsync()) { }

        // v1'i retire et
        await store.RetireKeyAsync(nameof(WebhookTestContext), 1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }
}