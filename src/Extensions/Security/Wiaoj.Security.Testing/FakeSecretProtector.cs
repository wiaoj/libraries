using System.Security.Cryptography;
using System.Text;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.Security.Testing;

/// <summary>
/// A non-cryptographic, predictable implementation of <see cref="ISecretProtector{TContext}"/>
/// for use in unit tests. Does NOT provide real security.
/// </summary>
/// <typeparam name="TContext">The secret context.</typeparam>
public sealed class FakeSecretProtector<TContext> : ISecretProtector<TContext> where TContext : ISecretContext {
    private const int DummyHeaderLength = 28; // 12 Nonce + 16 Tag = 28 bytes to satisfy CipherBlob validation

    /// <summary>
    /// If set to true, any Protect/Unprotect call will throw a <see cref="CryptographicException"/>.
    /// Useful for testing failure scenarios.
    /// </summary>
    public bool ShouldFail { get; set; }

    /// <summary>
    /// The key version used for new <see cref="Protect(string)"/> calls.
    /// Can be changed during tests to simulate rotation.
    /// </summary>
    public int ActiveVersion { get; set; } = 1;

    /// <summary>Fixed key version used for all "protections" in this fake.</summary>
    public KeyVersion CurrentKeyVersion => KeyVersion.Of(this.ActiveVersion);

    /// <summary>
    /// Simply encodes the plaintext with a dummy AES-GCM header. No real encryption occurs.
    /// </summary>
    public EncryptedSecret<TContext> Protect(string plaintext) {
        Preca.ThrowIfNull(plaintext);
        if(this.ShouldFail) throw new CryptographicException("Fake failure triggered.");

        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        return Protect(plainBytes.AsSpan());
    }

    /// <summary>
    /// Simply encodes the plaintext bytes with a dummy AES-GCM header. No real encryption occurs.
    /// </summary>
    public EncryptedSecret<TContext> Protect(ReadOnlySpan<byte> plaintextBytes) {
        Preca.ThrowIfEmpty(plaintextBytes);
        if(this.ShouldFail) throw new CryptographicException("Fake failure triggered.");

        // 28 byte sahte header + plaintext ekleyerek CipherBlob'un 38 karakter sınırını geçmesini sağlıyoruz:
        byte[] fakePacket = new byte[DummyHeaderLength + plaintextBytes.Length];
        plaintextBytes.CopyTo(fakePacket.AsSpan(DummyHeaderLength));

        CipherBlob blob = CipherBlob.From(Base64UrlString.FromBytes(fakePacket));
        return EncryptedSecret<TContext>.Create(blob, keyVersion: this.CurrentKeyVersion);
    }

    /// <summary>
    /// Decodes the fake ciphertext back to plaintext by stripping the dummy header.
    /// </summary>
    public Secret<byte> Unprotect(in EncryptedSecret<TContext> encrypted) {
        if(this.ShouldFail) throw new CryptographicException("Fake failure triggered.");

        byte[] fakePacket = Base64UrlString.Parse(encrypted.Blob.RawBase64Url).ToBytes();
        try {
            // Başındaki 28 byte sahte header'ı atıp orijinal plaintext'i çıkarıyoruz:
            ReadOnlySpan<byte> plainSpan = fakePacket.AsSpan(DummyHeaderLength);
            return Secret.From(plainSpan);
        }
        finally {
            CryptographicOperations.ZeroMemory(fakePacket);
        }
    }

    /// <summary>Returns true when the encrypted data's version is older than the current <see cref="ActiveVersion"/>.</summary>
    public bool NeedsRotation(in EncryptedSecret<TContext> encrypted) {
        return encrypted.KeyVersion.Value < this.ActiveVersion;
    }

    /// <summary>Re-protects the data using the current <see cref="ActiveVersion"/>.</summary>
    public EncryptedSecret<TContext> Rotate(in EncryptedSecret<TContext> encrypted) {
        if(!NeedsRotation(encrypted)) return encrypted;

        using Secret<byte> plain = Unprotect(encrypted);
        return plain.Expose(Protect);
    }
}