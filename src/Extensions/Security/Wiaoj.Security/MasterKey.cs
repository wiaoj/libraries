using System.Security.Cryptography;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Security;

/// <summary>
/// A strongly-typed wrapper around the master key.
/// Prevents accidentally passing a regular <see cref="Secret{T}" /> where the master key is expected.
/// </summary>
public readonly struct MasterKey(Secret<byte> secret) : IDisposable {

    /// <summary>Provides scoped access to the underlying master key bytes.</summary>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        secret.Expose(action);
    }

    /// <summary>Provides scoped access to the underlying master key bytes and returns a result.</summary>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        return secret.Expose(func);
    }

    /// <summary>
    /// Wraps (encrypts) raw key material using the master key.
    /// Wire format is owned by <see cref="AesGcmKey"/>:
    /// nonce[12] | auth_tag[16] | ciphertext[N], then Base64Url-encoded.
    /// </summary>
    public string Wrap(ReadOnlySpan<byte> keyMaterial) {
        // ✅ From(ReadOnlySpan) — copies master key bytes into AesGcmKey's own memory.
        //    Does NOT take ownership of `secret`. After using block, only AesGcmKey's
        //    internal copy is zeroed; `secret` stays intact for future calls.
        return secret.Expose(keyMaterial, (key, masterSpan) => {
            using AesGcmKey aesKey = AesGcmKey.From(masterSpan);
            byte[] packet = aesKey.Encrypt(key);
            try {
                return Base64UrlString.FromBytes(packet);
            }
            finally {
                CryptographicOperations.ZeroMemory(packet);
            }
        });
    }

    /// <summary>
    /// Decrypts a wrapped key material blob.
    /// Returns a <see cref="Secret{T}"/> backed by secure unmanaged memory. Caller must dispose.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// Thrown when the blob is malformed or authentication fails (wrong master key / tampering).
    /// </exception>
    public Secret<byte> Unwrap(string wrappedBase64) {
        // Decode once, keep reference — so finally zeroes the same array used in Decrypt.
        // ❌ Eski: combined.ToBytes() finally'de yeni array yaratıyordu, orijinali sıfırlamıyordu.
        byte[] packet = Base64UrlString.Parse(wrappedBase64).ToBytes();
        try {
            return secret.Expose(masterSpan => {
                try { 
                    using AesGcmKey aesKey = AesGcmKey.From(masterSpan);
                    return aesKey.Decrypt(packet);
                }
                catch(CryptographicException ex) {
                    throw new CryptographicException(
                        "Master key authentication failed while unwrapping a key. " +
                        "The master key may be incorrect or the stored blob may be corrupt.", ex);
                }
            });
        }
        finally {
            CryptographicOperations.ZeroMemory(packet);
        }
    }

    /// <summary>
    /// Convenience: unwraps the wrapped key material and returns it as an <see cref="EncryptionKey"/>.
    /// </summary>
    public EncryptionKey UnwrapToKey(string wrappedBase64, KeyVersion version, bool isRetired) {
        Secret<byte> unwrapped = default;
        try {
            unwrapped = Unwrap(wrappedBase64);

            // AesGcmKey.From(Secret<byte>) — burada ownership transfer DOĞRU.
            // unwrapped artık aesKey'e ait; EncryptionKey de aesKey'i dispose edecek.
            AesGcmKey aesKey = AesGcmKey.From(unwrapped);
            return new EncryptionKey(version, aesKey, isRetired);
        }
        catch {
            if(unwrapped != default)
                unwrapped.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        secret.Dispose();
    }
}