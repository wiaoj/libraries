using System.Text;
using Wiaoj.Primitives;
using System.Security.Cryptography;

namespace Wiaoj.Security.Testing;

/// <summary>
/// A non-cryptographic, predictable implementation of <see cref="ISecretProtector{TContext}"/>
/// for use in unit tests. Does NOT provide real security.
/// </summary>
/// <typeparam name="TContext">The secret context.</typeparam>
public sealed class FakeSecretProtector<TContext> : ISecretProtector<TContext> where TContext : ISecretContext {

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
    public KeyVersion CurrentKeyVersion => KeyVersion.Of(ActiveVersion);

    /// <summary>
    /// Simply Base64-encodes the plaintext. No real encryption occurs.
    /// </summary>
    public EncryptedSecret<TContext> Protect(string plaintext) {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (ShouldFail) throw new CryptographicException("Fake failure triggered.");

        CipherBlob blob = CipherBlob.From(Base64UrlString.FromBytes(Encoding.UTF8.GetBytes(plaintext)));
        return EncryptedSecret<TContext>.Create(blob, keyVersion: this.CurrentKeyVersion);
    }

    /// <summary>
    /// Simply Base64-encodes the plaintext. No real encryption occurs.
    /// </summary>
    public EncryptedSecret<TContext> Protect(ReadOnlySpan<byte> plaintextBytes) {
        if(plaintextBytes.IsEmpty)
            throw new ArgumentException("Plaintext bytes cannot be empty.", nameof(plaintextBytes));
        
        if (ShouldFail) throw new CryptographicException("Fake failure triggered.");

        CipherBlob blob = CipherBlob.From(Base64UrlString.FromBytes(plaintextBytes));
        return EncryptedSecret<TContext>.Create(blob, keyVersion: this.CurrentKeyVersion);
    }

    /// <summary>
    /// Decodes the Base64 "ciphertext" back to plaintext.
    /// </summary>
    public Secret<byte> Unprotect(in EncryptedSecret<TContext> encrypted) {
        if (ShouldFail) throw new CryptographicException("Fake failure triggered.");

        // We assume the blob was created by this fake (Base64Url of plaintext)
        Base64UrlString raw = Base64UrlString.Parse(encrypted.Blob.RawBase64);
        return Secret.From(raw.ToBytes());
    }

    /// <summary>Returns true when the encrypted data's version is older than the current <see cref="ActiveVersion"/>.</summary>
    public bool NeedsRotation(in EncryptedSecret<TContext> encrypted) => encrypted.KeyVersion.Value < ActiveVersion;

    /// <summary>Re-protects the data using the current <see cref="ActiveVersion"/>.</summary>
    public EncryptedSecret<TContext> Rotate(in EncryptedSecret<TContext> encrypted) {
        if (!NeedsRotation(encrypted)) return encrypted;
        
        using Secret<byte> plain = Unprotect(encrypted);
        return plain.Expose(Protect);
    }
}