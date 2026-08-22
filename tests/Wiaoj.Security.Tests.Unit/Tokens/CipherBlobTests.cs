namespace Wiaoj.Security.Tests.Unit.Tokens;

[Trait("Category", "Unit")]
[Trait("Feature", "Tokens")]
public class CipherBlobTests {

    [Fact]
    public void From_WithValidBase64UrlString_ShouldCreateCipherBlob() {
        // Arrange (Min 28 byte AES-GCM paketi -> 38 karakter)
        byte[] validPacket = new byte[28];
        Base64UrlString encoded = Base64UrlString.FromBytes(validPacket);

        // Act
        CipherBlob blob = CipherBlob.From(encoded);

        // Assert
        Assert.Equal(encoded.Value, blob.ToStorageString());
    }

    [Fact]
    public void From_WithShortString_ShouldThrowArgumentException() {
        // Arrange (28 byte'tan kısa paket)
        byte[] shortPacket = new byte[10];
        Base64UrlString encoded = Base64UrlString.FromBytes(shortPacket);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CipherBlob.From(encoded));
    }

    [Fact]
    public void FromStorageString_WithNullOrWhitespace_ShouldThrowArgumentException() {
        // Act & Assert
        Assert.ThrowsAny<ArgumentNullException>(() => CipherBlob.FromStorageString(null!));
        Assert.ThrowsAny<ArgumentException>(() => CipherBlob.FromStorageString(""));
        Assert.ThrowsAny<ArgumentException>(() => CipherBlob.FromStorageString("   "));
    }

    [Fact]
    public void ToString_ShouldNeverExposeRawCiphertext() {
        // Arrange
        byte[] validPacket = new byte[32];
        Base64UrlString encoded = Base64UrlString.FromBytes(validPacket);
        CipherBlob blob = CipherBlob.From(encoded);

        // Act
        string display = blob.ToString();

        // Assert
        Assert.Equal("[CIPHER_BLOB]", display);
        Assert.DoesNotContain(encoded.Value, display);
    }
}