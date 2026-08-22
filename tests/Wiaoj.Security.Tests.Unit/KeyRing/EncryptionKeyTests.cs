using System.Security.Cryptography;
using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Security.Tests.Unit.KeyRing;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyRing")]
public class EncryptionKeyTests {

    private static EncryptionKey CreateKey(int version = 1, bool isRetired = false) {
        AesGcmKey aesKey = AesGcmKey.Generate256();
        return new EncryptionKey(KeyVersion.Of(version), aesKey, isRetired);
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldRoundtripPlaintextCorrectly() {
        // Arrange
        using EncryptionKey key = CreateKey();
        byte[] plaintext = "hello-security-key"u8.ToArray();
        byte[] aad = "test-aad-context"u8.ToArray();

        // Act
        byte[] packet = key.Encrypt(plaintext, aad);
        using Secret<byte> decrypted = key.Decrypt(packet, aad);

        // Assert
        decrypted.Expose(span => {
            Assert.True(plaintext.AsSpan().SequenceEqual(span));
        });
    }

    [Fact]
    public void Decrypt_WithWrongAad_ShouldThrowCryptographicException() {
        // Arrange
        using EncryptionKey key = CreateKey();
        byte[] plaintext = "sensitive-data"u8.ToArray();
        byte[] correctAad = "context-a"u8.ToArray();
        byte[] wrongAad = "context-b"u8.ToArray();

        byte[] packet = key.Encrypt(plaintext, correctAad);

        // Act & Assert
        Assert.Throws<CryptographicException>(() => key.Decrypt(packet, wrongAad));
    }

    [Fact]
    public void Dispose_ShouldPreventFurtherCryptographicOperations() {
        // Arrange
        EncryptionKey key = CreateKey();
        key.Dispose();

        byte[] data = "some-data"u8.ToArray();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => key.Encrypt(data));
        Assert.Throws<ObjectDisposedException>(() => key.Decrypt(data));
    }

    [Fact]
    public void ToString_ShouldBeLogSafe() {
        // Arrange
        using EncryptionKey activeKey = CreateKey(1, isRetired: false);
        using EncryptionKey retiredKey = CreateKey(2, isRetired: true);

        // Act & Assert
        Assert.Equal("[KEY v1 ACTIVE]", activeKey.ToString());
        Assert.Equal("[KEY v2 RETIRED]", retiredKey.ToString());
    }
}