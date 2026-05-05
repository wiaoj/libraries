using System.Security.Cryptography;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Security;

/// <summary>
/// A strongly-typed wrapper around the master key.
/// Prevents accidentally passing a regular <see cref="Secret{T}" /> where the master key is expected.
/// </summary>
public readonly struct MasterKey(Secret<byte> secret) : IDisposable {

    /// <summary>
    /// Provides scoped access to the underlying master key bytes.
    /// </summary>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        secret.Expose(action);
    }

    /// <summary>
    /// Provides scoped access to the underlying master key bytes and returns a result.
    /// </summary>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        return secret.Expose(func);
    }


    /// <summary>
    /// Wraps (encrypts) and unwraps (decrypts) raw key material using the master key.
    /// Delegates all cipher work to <see cref="AesGcmKey"/>. Internal only.
    /// </summary>
    /// <remarks>
    /// Wire format is owned by <see cref="AesGcmKey"/>:
    /// nonce[12] | auth_tag[16] | ciphertext[N], then Base64-encoded.
    /// </remarks>
    public string Wrap(ReadOnlySpan<byte> keyMaterial) {
        // AesGcmKey.From copies masterKey into secure unmanaged memory and is disposed
        // immediately after use — key material never lingers on the managed heap.
        using AesGcmKey aesKey = AesGcmKey.From(secret);
        byte[] packet = aesKey.Encrypt(keyMaterial);
        try {
            return Base64UrlString.FromBytes(packet);
        }
        finally {
            CryptographicOperations.ZeroMemory(packet);
        }
    }

    /// <summary>
    /// Decrypts a wrapped key material blob.
    /// Returns a <see cref="Secret{T}"/> backed by secure unmanaged memory. Caller must dispose.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// Thrown when the blob is malformed or authentication fails (wrong master key / tampering).
    /// </exception>
    public Secret<byte> Unwrap(string wrappedBase64) {
        Base64UrlString combined = Base64UrlString.Parse(wrappedBase64);
        try {
            using AesGcmKey aesKey = AesGcmKey.From(secret);
            try {
                // AesGcmKey.Decrypt validates packet length and wraps
                // AuthenticationTagMismatchException as CryptographicException.
                return aesKey.Decrypt(combined.ToBytes());
            }
            catch(CryptographicException ex) {
                // Re-throw with a message that pinpoints the master-key layer,
                // so the caller can distinguish it from application-level decryption errors.
                throw new CryptographicException(
                    "Master key authentication failed while unwrapping a key. " +
                    "The master key may be incorrect or the stored blob may be corrupt.", ex);
            }
        }
        finally {
            CryptographicOperations.ZeroMemory(combined.ToBytes());
        }
    }

    public EncryptionKey UnwrapToKey(string wrappedBase64, KeyVersion version, bool isRetired) {
        Secret<byte> secret = default;
        try {
            // 1. MasterKey ile anahtarı çöz
            secret = Unwrap(wrappedBase64);

            // 2. Ham secret'ı motor (AesGcmKey) haline getir
            AesGcmKey aesKey = AesGcmKey.From(secret);

            // 3. İkisini birleştirip EncryptionKey (DEK) döndür
            return new EncryptionKey(version, aesKey, isRetired);
        }
        catch(Exception) {
            if(secret != default) {
                secret.Dispose();
            }
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        secret.Dispose();
    }
}