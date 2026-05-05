using System.Diagnostics;
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

    // Cache tag to avoid allocating a new KVP on every encrypt/decrypt call.
    private static readonly KeyValuePair<string, object?> _contextTag = SecurityMeter.ContextTag<TContext>();


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

        long start = Stopwatch.GetTimestamp();
        try {

            EncryptionKey currentKey = EnsureActiveKey();
            byte[] packet = currentKey.Encrypt(plainSecret);

            try {
                Base64UrlString encoded = Base64UrlString.FromBytes(packet);
                EncryptedSecret<TContext> result = EncryptedSecret<TContext>.Create(
                CipherBlob.From(encoded),
                this._keyRing.CurrentVersion);

                SecurityMeter.ProtectCount.Add(1, _contextTag);
                SecurityMeter.ProtectDuration.Record(
                    Stopwatch.GetElapsedTime(start).TotalMilliseconds, _contextTag);

                return result;
            }
            finally {
                CryptographicOperations.ZeroMemory(packet);
            }
        }
        catch {
            SecurityMeter.ProtectErrorCount.Add(1, _contextTag);
            throw;
        }
    }

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Protect(string plainText) {
        Preca.ThrowIfNull(plainText);
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

        long start = Stopwatch.GetTimestamp();
        try {
            EncryptionKey key = this._keyRing.GetKey(encrypted.KeyVersion);

            byte[] combined = Base64UrlString.Parse(encrypted.Blob.RawBase64).ToBytes();
            try {
                Secret<byte> result = key.Decrypt(combined);

                SecurityMeter.UnprotectCount.Add(1, _contextTag);
                SecurityMeter.UnprotectDuration.Record(
                    Stopwatch.GetElapsedTime(start).TotalMilliseconds, _contextTag);

                return result;
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
        catch {
            SecurityMeter.UnprotectErrorCount.Add(1, _contextTag);
            throw;
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