using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Symmetric;

namespace Wiaoj.Security;

/// <summary>
/// A versioned encryption key entry within a <see cref="KeyRing{TContext}"/>.
/// Wraps an <see cref="AesGcmKey"/> and delegates all cipher operations to it —
/// no raw key exposure or manual nonce/tag handling required.
/// </summary>
public sealed class EncryptionKey : IDisposable {

    private readonly AesGcmKey _key;
    private readonly DisposeState _disposeState = new();

    /// <summary>The strongly-typed version of this key.</summary>
    public KeyVersion Version { get; }

    /// <summary>
    /// When <see langword="true"/>, this key can only decrypt existing data.
    /// It is never used for new encryptions.
    /// </summary>
    public bool IsRetired { get; }

    internal EncryptionKey(KeyVersion version, AesGcmKey key, bool isRetired) {
        this.Version = version;
        this._key = key;
        this.IsRetired = isRetired;
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns a packet:
    /// <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </summary>
    internal byte[] Encrypt(ReadOnlySpan<byte> plaintext) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EncryptionKey));
        return this._key.Encrypt(plaintext);
    }

    /// <summary>
    /// Decrypts a packet produced by <see cref="Encrypt"/>.
    /// Returns a <see cref="Secret{T}"/> in secure unmanaged memory — caller must dispose.
    /// </summary>
    internal Secret<byte> Decrypt(ReadOnlySpan<byte> packet) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EncryptionKey));
        return this._key.Decrypt(packet);
    }

    /// <summary>Securely erases the key material. After disposal this instance must not be used.</summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._key.Dispose();

            this._disposeState.SetDisposed();
        }
    }

    /// <summary>Safe for logging.</summary>
    public override string ToString() {
        return $"[KEY {this.Version}{(this.IsRetired ? " RETIRED" : " ACTIVE")}]";
    }
}