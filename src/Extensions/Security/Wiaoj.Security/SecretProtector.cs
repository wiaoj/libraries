using System.Security.Cryptography;
using System.Text;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// AES-GCM–based implementation of <see cref="ISecretProtector{TContext}"/>.
/// Delegates all cipher operations directly to <see cref="EncryptionKey"/> —
/// no manual key exposure, no AesGcmKey construction inside callbacks.
/// </summary>
public sealed class SecretProtector<TContext> : ISecretProtector<TContext>, IDisposable
    where TContext : ISecretContext {

    private readonly KeyRing<TContext> _keyRing;
    private readonly DisposeState _disposeState = new();

    /// <param name="keyRing">The key ring. This protector takes ownership and disposes it.</param>
    public SecretProtector(KeyRing<TContext> keyRing) {
        Preca.ThrowIfNull(keyRing);
        this._keyRing = keyRing;
    }

    /// <inheritdoc/>
    public KeyVersion CurrentKeyVersion {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(SecretProtector<>));
            return this._keyRing.CurrentVersion;
        }
    }

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Protect(ReadOnlySpan<byte> plainSecret) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(SecretProtector<>));

        EncryptionKey currentKey = EnsureActiveKey();
        byte[] packet = currentKey.Encrypt(plainSecret);

        try {
            return EncryptedSecret<TContext>.Create(
                CipherBlob.FromEncryptionResult(Base64String.Parse(packet)),
                this._keyRing.CurrentVersion);
        }
        finally {
            CryptographicOperations.ZeroMemory(packet);
        }
    }

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Protect(string plainText) {
        ArgumentNullException.ThrowIfNull(plainText);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(SecretProtector<>));

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        try {
            return Protect(plainBytes.AsSpan());
        }
        finally {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    /// <inheritdoc/>
    public Secret<byte> Unprotect(in EncryptedSecret<TContext> encrypted) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(SecretProtector<>));

        EncryptionKey key = this._keyRing.GetKey(encrypted.KeyVersion);

        byte[] combined = Convert.FromBase64String(encrypted.Blob.RawBase64);
        try {
            return key.Decrypt(combined);
        }
        catch(CryptographicException ex) when(ex.InnerException is AuthenticationTagMismatchException) {
            throw new CryptographicException(
                $"Authentication tag mismatch for EncryptedSecret<{typeof(TContext).Name}> " +
                $"(key {encrypted.KeyVersion}). Data may be corrupt or tampered.", ex);
        }
        finally {
            CryptographicOperations.ZeroMemory(combined);
        }
    }

    /// <inheritdoc/>
    public bool NeedsRotation(in EncryptedSecret<TContext> encrypted) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(SecretProtector<>));
        return this._keyRing.NeedsRotation(encrypted.KeyVersion);
    }

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Rotate(in EncryptedSecret<TContext> encrypted) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(SecretProtector<>));
        if(!NeedsRotation(encrypted)) return encrypted;

        using Secret<byte> plain = Unprotect(encrypted);
        return plain.Expose(Protect);
    }

    /// <summary>Disposes this protector and its key ring, zeroing all key material.</summary> 
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._keyRing.Dispose();

            this._disposeState.SetDisposed();
        }
    }
     
    private EncryptionKey EnsureActiveKey() {
        EncryptionKey currentKey = this._keyRing.CurrentKey;
        if(currentKey.IsRetired)
            throw new InvalidOperationException(
                $"Cannot encrypt with key {currentKey.Version}: it is marked as retired.");
        return currentKey;
    }
}