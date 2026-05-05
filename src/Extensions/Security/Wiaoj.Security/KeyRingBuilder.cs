using Wiaoj.Preconditions;

namespace Wiaoj.Security;

/// <summary>
/// Fluent builder for constructing a <see cref="KeyRing{TContext}"/>.
/// </summary>
public sealed class KeyRingBuilder<TContext> where TContext : ISecretContext {
    private readonly Dictionary<int, EncryptionKey> _entries = [];
    private KeyVersion? _currentVersion;

    // ── Fluent API ────────────────────────────────────────────────────────────

    /// <summary>Registers the active key used for encrypting new secrets.</summary> 
    /// <param name="encryptionKey">The AES key. Ownership transfers to this builder.</param>
    public KeyRingBuilder<TContext> WithCurrentKey(EncryptionKey encryptionKey) {
        if(this._currentVersion.HasValue)
            throw new InvalidOperationException(
                "A current key has already been registered. Only one active key is allowed per ring.");

        ValidateAndAdd(encryptionKey);
        this._currentVersion = encryptionKey.Version;
        return this;
    }

    /// <summary>
    /// Registers a retired key that can only decrypt old data, not produce new ciphertexts.
    /// </summary>
    public KeyRingBuilder<TContext> WithRetiredKey(EncryptionKey encryptionKey) {
        ValidateAndAdd(encryptionKey);
        return this;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    /// <summary>Constructs the <see cref="KeyRing{TContext}"/>.</summary>
    public KeyRing<TContext> Build() {
        if(!this._currentVersion.HasValue)
            throw new InvalidOperationException(
                $"Cannot build a KeyRing<{typeof(TContext).Name}> without a current key. " +
                "Call WithCurrentKey() before Build().");

        return new KeyRing<TContext>(new Dictionary<int, EncryptionKey>(this._entries), this._currentVersion.Value);
    }

    /// <summary>Convenience: <see cref="Build" /> followed by <see cref="KeyRingExtensions.CreateProtector{TContext}" />.</summary>
    public SecretProtector<TContext> BuildProtector() {
        return Build().CreateProtector();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ValidateAndAdd(EncryptionKey encryptionKey) {
        Preca.ThrowIfTrue(
            this._entries.ContainsKey(encryptionKey.Version),
            (version) => new ArgumentException($"A key with version {encryptionKey.Version} is already registered."),
            encryptionKey.Version);


        this._entries[encryptionKey.Version] = encryptionKey;
    }
}