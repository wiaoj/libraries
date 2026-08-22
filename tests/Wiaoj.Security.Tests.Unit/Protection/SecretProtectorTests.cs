using System.Security.Cryptography;

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