using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Primitives.Tests.Unit.Cryptography;

public class AesGcmKeyComprehensiveTests : IDisposable {
    private readonly AesGcmKey _defaultKey = AesGcmKey.Generate256();

    public void Dispose() {
        this._defaultKey.Dispose();
    }

    [Fact] public void Generate_128_ProducesCorrectSize() { using AesGcmKey k = AesGcmKey.Generate128(); Assert.Equal(16, (int)k.KeySize); }
    [Fact] public void Generate_192_ProducesCorrectSize() { using AesGcmKey k = AesGcmKey.Generate192(); Assert.Equal(24, (int)k.KeySize); }
    [Fact] public void Generate_256_ProducesCorrectSize() { using AesGcmKey k = AesGcmKey.Generate256(); Assert.Equal(32, (int)k.KeySize); }

    [Theory]
    [InlineData(0), InlineData(1), InlineData(15), InlineData(17), InlineData(31), InlineData(33)]
    public void From_InvalidRawBytes_Throws(int size) { Assert.Throws<ArgumentException>(() => AesGcmKey.From(new byte[size])); }

    [Fact]
    public void From_DefaultSecret_ThrowsOrHandles() {
        Secret<byte> defaultSecret = default;
        Assert.Throws<ArgumentException>(() => AesGcmKey.From(defaultSecret));
    }

    [Fact] public void TryFrom_ValidBytes_ReturnsTrue() { Assert.True(AesGcmKey.TryFrom(new byte[16], out _)); }
    [Fact] public void TryFrom_InvalidBytes_ReturnsFalse() { Assert.False(AesGcmKey.TryFrom(new byte[10], out _)); }


    [Fact]
    public void Encrypt_Decrypt_FullDataIntegrity() {
        byte[] input = "Test Verisi 123456"u8.ToArray();
        byte[] encrypted = this._defaultKey.Encrypt(input);
        using Secret<byte> secret = this._defaultKey.Decrypt(encrypted);
        secret.Expose(span => Assert.True(input.AsSpan().SequenceEqual(span)));
    }

    [Fact]
    public void Decrypt_WithAAD_Success() {
        byte[] aad = "header"u8.ToArray();
        byte[] enc = this._defaultKey.Encrypt("test"u8.ToArray(), aad);
        using Secret<byte> dec = this._defaultKey.Decrypt(enc, aad);
        Assert.True(true);
    }

    [Fact]
    public void Decrypt_WithWrongAAD_Throws() {
        byte[] enc = this._defaultKey.Encrypt("test"u8.ToArray(), "aad1"u8.ToArray());
        Assert.Throws<CryptographicException>(() => this._defaultKey.Decrypt(enc, "aad2"u8.ToArray()));
    }

    [Fact]
    public void Decrypt_TamperedNonce_Throws() {
        byte[] enc = this._defaultKey.Encrypt("test"u8.ToArray());
        enc[0] ^= 0x01; // replace Nonce
        Assert.Throws<CryptographicException>(() => this._defaultKey.Decrypt(enc));
    }

    [Fact]
    public void Decrypt_TamperedTag_Throws() {
        byte[] enc = this._defaultKey.Encrypt("test"u8.ToArray());
        enc[12] ^= 0x01; // replace Tag
        Assert.Throws<CryptographicException>(() => this._defaultKey.Decrypt(enc));
    }

    [Fact]
    public void DisposedKey_ThrowsOnAllOperations() {
        AesGcmKey key = AesGcmKey.Generate128();
        key.Dispose();
        Assert.Throws<ObjectDisposedException>(() => key.KeySize);
        Assert.Throws<ObjectDisposedException>(() => key.Encrypt("test"));
        Assert.Throws<ObjectDisposedException>(() => key.Decrypt(new byte[32]));
        Assert.Throws<ObjectDisposedException>(() => key.Expose(_ => { }));
    }

    [Fact]
    public void DoubleDispose_IsSafe() {
        AesGcmKey key = AesGcmKey.Generate128();
        key.Dispose();
        key.Dispose();
    }

    [Theory]
    [InlineData("UTF-8 Test", "UTF-8")]
    [InlineData("🚀🔥", "UTF-8")]
    public void EncryptDecrypt_Strings(string text, string encoding) {
        byte[] enc = this._defaultKey.Encrypt(text, Encoding.GetEncoding(encoding));
        string dec = this._defaultKey.DecryptToString(enc, Encoding.GetEncoding(encoding));
        Assert.Equal(text, dec);
    }

    [Fact]
    public void Encrypt_NullString_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(() => this._defaultKey.Encrypt((string)null!));
    }

    [Fact]
    public async Task EncryptAsync_Stream_Works() {
        MemoryStream ms = new(Encoding.UTF8.GetBytes("stream-test"));
        byte[] enc = await this._defaultKey.EncryptAsync(ms);
        using Secret<byte> dec = this._defaultKey.Decrypt(enc);
        dec.Expose(s => Assert.Equal("stream-test", Encoding.UTF8.GetString(s)));
    }

    [Fact]
    public async Task DecryptToAsync_Stream_Works() {
        byte[] enc = this._defaultKey.Encrypt("stream-test"u8.ToArray());
        using MemoryStream ms = new();
        await this._defaultKey.DecryptToAsync(enc, ms);
        Assert.Equal("stream-test", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public void ToString_ReturnsSentinel() {
        Assert.Contains("AES_GCM_KEY", this._defaultKey.ToString());
        Assert.DoesNotContain(this._defaultKey.ToHexString().ToString(), this._defaultKey.ToString());
    }

    [Fact]
    public void KeyExposure_NeverLeaksOutSideAction() {
        bool accessed = false;
        this._defaultKey.Expose(span => {
            Assert.Equal(32, span.Length);
            accessed = true;
        });
        Assert.True(accessed);
    }

    [Fact]
    public void ImportExport_RoundTrip() {
        HexString hex = this._defaultKey.ToHexString();
        using AesGcmKey key2 = AesGcmKey.From(hex);
        Assert.Equal(this._defaultKey.KeySize, key2.KeySize);
    }

    [Fact]
    public void Encrypt_VeryLargeData_Works() {
        byte[] large = new byte[1024 * 64]; // 64KB
        RandomNumberGenerator.Fill(large);
        byte[] enc = this._defaultKey.Encrypt(large);
        using Secret<byte> dec = this._defaultKey.Decrypt(enc);
        dec.Expose(s => Assert.True(large.AsSpan().SequenceEqual(s)));
    }

    [Fact]
    public void Encrypt_EmptyPlaintext_Works() {
        byte[] enc = this._defaultKey.Encrypt([]);
        Assert.Equal(12 + 16, enc.Length); // nonce + tag
        using Secret<byte> dec = this._defaultKey.Decrypt(enc);
        dec.Expose(s => Assert.Empty(s.ToArray()));
    }
}