namespace Wiaoj.Security;

/// <summary>
/// An immutable collection of versioned <see cref="EncryptionKey"/> objects
/// that belong to a single secret domain (<typeparamref name="TContext"/>).
/// </summary>
public sealed class KeyRing<TContext> : IDisposable
    where TContext : ISecretContext {

    private readonly IReadOnlyDictionary<int, EncryptionKey> _keys;
    private bool _disposed;

    /// <summary>The version of the key currently used for new encryptions.</summary>
    public KeyVersion CurrentVersion { get; }

    /// <summary>Total number of key versions registered (active + retired).</summary>
    public int Count => _keys.Count;

    internal KeyRing(IReadOnlyDictionary<int, EncryptionKey> keys, KeyVersion currentVersion) {
        _keys = keys;
        CurrentVersion = currentVersion;
    }

    // ── Key retrieval ─────────────────────────────────────────────────────────

    /// <summary>Returns the currently active key (used for new encryptions).</summary>
    public EncryptionKey CurrentKey {
        get {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _keys[CurrentVersion];
        }
    }

    /// <summary>
    /// Retrieves the key with the specified version.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when <paramref name="version"/> is not registered.
    /// Ensure all historical versions are present until all data has been rotated.
    /// </exception>
    public EncryptionKey GetKey(KeyVersion version) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if(!_keys.TryGetValue(version, out EncryptionKey? key))
            throw new KeyNotFoundException(
                $"Key {version} is not registered in the key ring for " +
                $"context '{typeof(TContext).Name}'. " +
                "Add it as a retired key to continue decrypting old data.");

        return key;
    }

    // ── Rotation helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="version"/> is older than
    /// <see cref="CurrentVersion"/>.
    /// </summary>
    public bool NeedsRotation(KeyVersion version) => version < CurrentVersion;

    /// <summary>Returns <see langword="true"/> if the ring contains a key with this version.</summary>
    public bool ContainsVersion(KeyVersion version) => _keys.ContainsKey(version);

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>Disposes every key in the ring, securely zeroing all key material.</summary>
    public void Dispose() {
        if(!_disposed) {
            _disposed = true;
            foreach(EncryptionKey key in _keys.Values)
                key.Dispose();
        }
    }
}