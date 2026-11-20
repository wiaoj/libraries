using System.Security.Cryptography;
using Wiaoj.Security.Testing;

namespace Wiaoj.Security.Tests.Unit.Protection;

[Trait("Category", "Unit")]
[Trait("Feature", "Protection")]
public class SecretProtectorTests {

    private static (SecretProtector<TContext> Protector, MasterKey MasterKey) CreateProtector<TContext>(int keyVersion = 1)
        where TContext : ISecretContext {
        Secret<byte> masterSecret = Secret.Generate(32);
        MasterKey masterKey = new(masterSecret);

        byte[] dekBytes = RandomNumberGenerator.GetBytes(32);
        EncryptionKey dek = masterKey.UnwrapToKey(masterKey.Wrap(dekBytes), KeyVersion.Of(keyVersion), isRetired: false);

        KeyRing<TContext> ring = new KeyRingBuilder<TContext>()
            .WithCurrentKey(dek)
            .Build();

        return (new SecretProtector<TContext>(ring), masterKey);
    }

    [SecretContext(nameof(WebhookTestContext))]
    private sealed class RenamedWebhookContext : ISecretContext;

    private static class Billing {
        [SecretContext("billing-api")]
        public sealed class ApiContext : ISecretContext;
    }

    private static class Auth {
        [SecretContext("auth-api")]
        public sealed class ApiContext : ISecretContext;
    }

    [Fact]
    public void SameNamedContexts_WithDistinctIdentifiers_RejectEachOthersCiphertext() {
        using MasterKey master = new(Secret.Generate(32));
        string wrappedKey = master.Wrap(RandomNumberGenerator.GetBytes(32));
        SecretProtector<T> WithSameKey<T>() where T : ISecretContext => new(
            new KeyRingBuilder<T>().WithCurrentKey(master.UnwrapToKey(wrappedKey, KeyVersion.Of(1), false)).Build());
        using var billing = WithSameKey<Billing.ApiContext>();
        using var auth = WithSameKey<Auth.ApiContext>();
        var encrypted = billing.Protect("billing");
        var persisted = EncryptedSecret<Auth.ApiContext>.FromPersisted(encrypted.Blob, encrypted.KeyVersion);

        Assert.Throws<CryptographicException>(() => auth.Unprotect(persisted));
    }

    [Fact]
    public async Task RenamedContext_CanReadLegacyKeyRingAndCiphertext() {
        InMemoryEncryptionKeyStore store = new();
        FakeMasterKeyProvider master = new();
        var options = Microsoft.Extensions.Options.Options.Create(new KeyRotationOptions());
        KeyRingLoader<T> Loader<T>() where T : ISecretContext => new(store, master, options, TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;

        using SecretProtector<WebhookTestContext> original = new(await Loader<WebhookTestContext>().LoadAsync(ct));
        var encrypted = original.Protect("legacy");
        using SecretProtector<RenamedWebhookContext> renamed = new(await Loader<RenamedWebhookContext>().LoadAsync(ct));
        var persisted = EncryptedSecret<RenamedWebhookContext>.FromPersisted(encrypted.Blob.ToStorageString(), 1);
        using Secret<byte> plaintext = renamed.Unprotect(persisted);
        Assert.True(plaintext.Equals("legacy"u8));
        Assert.Equal(nameof(WebhookTestContext), SecretContextName<RenamedWebhookContext>.Value);
    }

    [Fact]
    public void ProtectAndUnprotect_String_ShouldRoundtripSuccessfully() {
        // Arrange
        (SecretProtector<WebhookTestContext>? protector, MasterKey masterKey) = CreateProtector<WebhookTestContext>();
        using(protector)
        using(masterKey) {
            string secretText = "my-super-secret-api-key-12345";

            // Act
            EncryptedSecret<WebhookTestContext> encrypted = protector.Protect(secretText);
            using Secret<byte> decrypted = protector.Unprotect(encrypted);

            // Assert
            string plainText = decrypted.Expose(span => System.Text.Encoding.UTF8.GetString(span));
            Assert.Equal(secretText, plainText);
            Assert.Equal(1, encrypted.KeyVersion.Value);
        }
    }

    [Fact]
    public void Unprotect_WhenCiphertextTampered_ShouldThrowCryptographicException() {
        // Arrange
        (SecretProtector<WebhookTestContext>? protector, MasterKey masterKey) = CreateProtector<WebhookTestContext>();
        using(protector)
        using(masterKey) {
            EncryptedSecret<WebhookTestContext> encrypted = protector.Protect("sensitive-payload");

            // Tamper
            string rawBlob = encrypted.Blob.RawBase64Url;
            char tamperedChar = rawBlob[^1] == 'A' ? 'B' : 'A';
            string tamperedBlobString = rawBlob[..^1] + tamperedChar;

            EncryptedSecret<WebhookTestContext> tamperedSecret = EncryptedSecret<WebhookTestContext>.FromPersisted(tamperedBlobString, encrypted.KeyVersion.Value);

            // Act & Assert
            Assert.Throws<CryptographicException>(() => protector.Unprotect(tamperedSecret));
        }
    }

    [Fact]
    public void Unprotect_WithMismatchedContextAAD_ShouldFailDecryption() {
        // Arrange
        (SecretProtector<WebhookTestContext>? webhookProtector, MasterKey masterKey1) = CreateProtector<WebhookTestContext>();
        EncryptedSecret<WebhookTestContext> webhookEncrypted;

        using(webhookProtector)
        using(masterKey1) {
            webhookEncrypted = webhookProtector.Protect("credit-card-number-1234");
        }

        (SecretProtector<PaymentTestContext>? paymentProtector, MasterKey masterKey2) = CreateProtector<PaymentTestContext>();
        using(paymentProtector)
        using(masterKey2) {
            EncryptedSecret<PaymentTestContext> forgedSecret = EncryptedSecret<PaymentTestContext>.FromPersisted(
                webhookEncrypted.Blob.ToStorageString(),
                webhookEncrypted.KeyVersion.Value);

            // Act & Assert
            Assert.Throws<CryptographicException>(() => paymentProtector.Unprotect(forgedSecret));
        }
    }

    [Fact]
    public void NeedsRotation_WhenEncryptedWithOlderKey_ShouldReturnTrue() {
        // Arrange
        Secret<byte> masterSecret = Secret.Generate(32);
        using MasterKey masterKey = new(masterSecret);

        EncryptionKey v1 = masterKey.UnwrapToKey(masterKey.Wrap(RandomNumberGenerator.GetBytes(32)), KeyVersion.Of(1), isRetired: true);
        EncryptionKey v2 = masterKey.UnwrapToKey(masterKey.Wrap(RandomNumberGenerator.GetBytes(32)), KeyVersion.Of(2), isRetired: false);

        KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(v1)
            .WithCurrentKey(v2)
            .Build();

        using SecretProtector<WebhookTestContext> protector = new(ring);

        // ✅ CS1503 Hatası Düzeltildi (KeyVersion.Of eklendi):
        EncryptedSecret<WebhookTestContext> v1Secret = EncryptedSecret<WebhookTestContext>.FromPersisted(
            CipherBlob.From(Base64UrlString.FromBytes(new byte[32])),
            KeyVersion.Of(1));

        EncryptedSecret<WebhookTestContext> v2Secret = EncryptedSecret<WebhookTestContext>.FromPersisted(
            CipherBlob.From(Base64UrlString.FromBytes(new byte[32])),
            KeyVersion.Of(2));

        // Act & Assert
        Assert.True(protector.NeedsRotation(v1Secret));
        Assert.False(protector.NeedsRotation(v2Secret));
    }
}
