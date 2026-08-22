using System.Security.Cryptography;

namespace Wiaoj.Security.Tests.Unit.KeyWrapping;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyWrapping")]
public class MasterKeyTests {

    [Theory]
    [InlineData(16)] // 128-bit AES
    [InlineData(24)] // 192-bit AES
    [InlineData(32)] // 256-bit AES
    public void WrapAndUnwrap_WithAllValidAesKeySizes_ShouldRoundtripCorrectly(int keySize) {
        // Arrange
        using Secret<byte> masterSecret = Secret.Generate(32);
        using MasterKey masterKey = new(masterSecret);

        byte[] originalKey = RandomNumberGenerator.GetBytes(keySize);

        // Act
        string wrapped = masterKey.Wrap(originalKey);
        using Secret<byte> unwrapped = masterKey.Unwrap(wrapped);

        // Assert
        unwrapped.Expose(span => {
            Assert.Equal(keySize, span.Length);
            Assert.True(originalKey.AsSpan().SequenceEqual(span));
        });
    }

    [Fact]
    public void Unwrap_WithTamperedAuthTagOrCiphertext_ShouldThrowCryptographicException() {
        // Arrange
        using Secret<byte> masterSecret = Secret.Generate(32);
        using MasterKey masterKey = new(masterSecret);

        byte[] originalKey = RandomNumberGenerator.GetBytes(32);
        string wrapped = masterKey.Wrap(originalKey);

        // Tamper: Son karakteri bozuyoruz
        char[] chars = wrapped.ToCharArray();
        chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
        string tamperedWrapped = new(chars);

        // Act & Assert
        Assert.Throws<CryptographicException>(() => masterKey.Unwrap(tamperedWrapped));
    }

    [Fact]
    public void Unwrap_WithDifferentMasterKey_ShouldThrowCryptographicException() {
        // Arrange
        using MasterKey keyA = new(Secret.Generate(32));
        using MasterKey keyB = new(Secret.Generate(32));

        byte[] dek = RandomNumberGenerator.GetBytes(32);
        string wrappedWithA = keyA.Wrap(dek);

        // Act & Assert
        Assert.Throws<CryptographicException>(() => keyB.Unwrap(wrappedWithA));
    }

    [Fact]
    public void UnwrapToKey_ShouldReturnFunctionalEncryptionKey() {
        // Arrange
        using MasterKey masterKey = new(Secret.Generate(32));
        byte[] dekBytes = RandomNumberGenerator.GetBytes(32);
        string wrapped = masterKey.Wrap(dekBytes);

        // Act
        using EncryptionKey dek = masterKey.UnwrapToKey(wrapped, KeyVersion.Of(1), isRetired: false);

        byte[] data = "test-payload"u8.ToArray();
        byte[] packet = dek.Encrypt(data);
        using Secret<byte> decrypted = dek.Decrypt(packet);

        // Assert
        Assert.Equal(1, dek.Version.Value);
        Assert.False(dek.IsRetired);
        decrypted.Expose(span => Assert.True(data.AsSpan().SequenceEqual(span)));
    }

    [Fact]
    public void Dispose_ShouldWipeMemoryAndPreventFurtherWrapUnwrap() {
        // Arrange
        Secret<byte> masterSecret = Secret.Generate(32);
        MasterKey masterKey = new(masterSecret);
        masterKey.Dispose();

        byte[] keyBytes = RandomNumberGenerator.GetBytes(32);
        string validBase64UrlBlob = Base64UrlString.FromBytes(new byte[32]).Value;

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => masterKey.Wrap(keyBytes));
        Assert.Throws<ObjectDisposedException>(() => masterKey.Unwrap(validBase64UrlBlob));
    }
}