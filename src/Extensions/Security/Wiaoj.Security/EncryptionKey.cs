using System.Security.Cryptography;
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
    internal byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EncryptionKey));
        return this._key.Encrypt(plaintext, associatedData);
    }

    /// <summary>
    /// Decrypts a packet produced by <see cref="Encrypt"/>.
    /// Returns a <see cref="Secret{T}"/> in secure unmanaged memory — caller must dispose.
    /// </summary>
    internal Secret<byte> Decrypt(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> associatedData = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EncryptionKey));
        return this._key.Decrypt(packet, associatedData);
    }

    /// <summary>The longest HKDF-SHA256 output: 255 hash blocks.</summary>
    internal const int MaxSubkeyLength = 255 * 32;

    private static readonly byte[] SubkeyInfoPrefix = "wiaoj.security.subkey:"u8.ToArray();

    /// <summary>
    /// Derives a subkey for <paramref name="purpose"/> into <paramref name="destination"/> with HKDF-SHA256 — the key
    /// itself is never exposed.
    /// </summary>
    /// <param name="purpose">A fixed, non-empty label naming what the subkey is for.</param>
    /// <param name="destination">Receives the subkey; 1 to 8,160 bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="purpose"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destination"/> is empty or too long.</exception>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <remarks>
    /// The HKDF info is <c>"wiaoj.security.subkey:" + purpose</c>, which separates subkeys from each other and from the
    /// key's own AES-GCM use. A retired key derives too, so data written under it stays readable.
    /// </remarks>
    public void DeriveSubkey(ReadOnlySpan<byte> purpose, Span<byte> destination) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EncryptionKey));
        if(purpose.IsEmpty) {
            throw new ArgumentException("A subkey needs a purpose, so subkeys for different uses are unrelated.", nameof(purpose));
        }

        ArgumentOutOfRangeException.ThrowIfZero(destination.Length, nameof(destination));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, MaxSubkeyLength, nameof(destination));

        byte[] info = [.. SubkeyInfoPrefix, .. purpose];
        int length = destination.Length;

        // AesGcmKey only lends its bytes to a callback, which cannot capture spans: derive into an array, copy, then wipe.
        byte[] subkey = this._key.Expose(key => HKDF.DeriveKey(HashAlgorithmName.SHA256, key.ToArray(), length, salt: null, info));
        try {
            subkey.CopyTo(destination);
        }
        finally {
            CryptographicOperations.ZeroMemory(subkey);
        }
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