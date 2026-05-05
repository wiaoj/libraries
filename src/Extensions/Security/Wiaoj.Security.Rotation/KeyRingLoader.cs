using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// Builds a <see cref="KeyRing{TContext}"/> by loading key records from the
/// <see cref="IEncryptionKeyStore"/> and unwrapping them with the master key.
/// If no keys exist for this context, bootstraps the first key automatically.
/// </summary>
/// <remarks>
/// Register as Scoped — it holds a reference to a scoped <see cref="IEncryptionKeyStore"/>.
/// </remarks>
public sealed class KeyRingLoader<TContext>
    where TContext : ISecretContext {
    private readonly IEncryptionKeyStore _store;
    private readonly IMasterKeyProvider _masterKeyProvider;
    private readonly KeyRotationOptions _options;
    private readonly string _contextName = typeof(TContext).Name;

    public KeyRingLoader(
        IEncryptionKeyStore store,
        IMasterKeyProvider masterKeyProvider,
        KeyRotationOptions options) {
        this._store = store;
        this._masterKeyProvider = masterKeyProvider;
        this._options = options;
    }

    /// <summary>
    /// Loads (or bootstraps) the <see cref="KeyRing{TContext}"/> for this context.
    /// </summary>
    public async Task<KeyRing<TContext>> LoadAsync(CancellationToken cancellationToken = default) {
        IReadOnlyList<EncryptionKeyRecord> records =
            await this._store.LoadKeysAsync(this._contextName, cancellationToken);

        if(records.Count == 0)
            return await BootstrapAsync(cancellationToken);

        using MasterKey masterKey = await this._masterKeyProvider.GetMasterKeyAsync(cancellationToken);
        return BuildKeyRing(records, masterKey);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>Generates and persists the very first key for this context.</summary>
    private async Task<KeyRing<TContext>> BootstrapAsync(CancellationToken cancellationToken) {
        byte[] keyMaterial = new byte[this._options.KeySizeInBytes];
        try {
            RandomNumberGenerator.Fill(keyMaterial);

            using MasterKey masterKey = await this._masterKeyProvider.GetMasterKeyAsync(cancellationToken);

            string wrapped = masterKey.Wrap(keyMaterial);

            EncryptionKeyRecord record = new() {
                Id = Guid.CreateVersion7(),
                ContextName = this._contextName,
                Version = 1,
                WrappedKeyMaterial = wrapped,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await this._store.SaveKeyAsync(record, cancellationToken);

            return BuildKeyRing([record], masterKey);
        }
        finally {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }

    private static KeyRing<TContext> BuildKeyRing(
        IReadOnlyList<EncryptionKeyRecord> records,
        MasterKey masterKey) {
        // Active key = highest version that is not retired
        EncryptionKeyRecord? active = records
            .Where(r => !r.IsRetired)
            .MaxBy(r => r.Version)
            ?? throw new InvalidOperationException(
                $"All keys for context '{typeof(TContext).Name}' are retired. " +
                "There must be at least one active key to build a key ring.");

        KeyRingBuilder<TContext> builder = new();

        foreach(EncryptionKeyRecord record in records) {
            // Unwrap: decrypt with master key → load into unmanaged memory

            EncryptionKey dek = masterKey.UnwrapToKey(record.WrappedKeyMaterial,
                                                      KeyVersion.Of(record.Version),
                                                      record.IsRetired);
             

            if(record.IsRetired)
                builder.WithRetiredKey(dek);
            else if(record.Version == active.Version)
                builder.WithCurrentKey(dek);
            // else: non-retired but not the latest active — shouldn't happen
            // but if it does, skip it (only one current key allowed per ring)
        }

        return builder.Build();
    }
}