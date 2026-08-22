using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Wiaoj.Security;

/// <summary>
/// Internal scoped loader that builds a <see cref="KeyRing{TContext}"/> by loading persisted key records from the
/// <see cref="IEncryptionKeyStore"/> and unwrapping their Data Encryption Keys (DEKs) using the <see cref="IMasterKeyProvider"/>.
/// </summary>
/// <typeparam name="TContext">The phantom type representing the secret's domain context.</typeparam>
/// <remarks>
/// <para>
/// <b>Automatic Bootstrapping:</b> If no keys exist in the database for the given context upon startup, 
/// <see cref="LoadAsync"/> automatically generates and wraps the first active key (version 1).
/// </para>
/// <para>
/// <b>Concurrency Protection:</b> Handles multi-node startup race conditions gracefully. If two or more application instances 
/// attempt to bootstrap version 1 concurrently, the losing instances safely catch the database unique constraint conflict 
/// and reload the winning instance's persisted key.
/// </para>
/// </remarks>
internal sealed class KeyRingLoader<TContext>(
    IEncryptionKeyStore store,
    IMasterKeyProvider masterKeyProvider,
    IOptions<KeyRotationOptions> options,
    TimeProvider timeProvider)
    where TContext : ISecretContext {

    private readonly KeyRotationOptions _options = options.Value;
    private readonly string _contextName = typeof(TContext).Name;

    /// <summary>
    /// Loads all active and retired key records from the store, unwraps them, and builds an immutable <see cref="KeyRing{TContext}"/>.
    /// If no keys exist, automatically bootstraps the first key.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the asynchronous operation to complete.</param>
    /// <returns>A fully initialized, immutable <see cref="KeyRing{TContext}"/> for this domain.</returns>
    /// <exception cref="InvalidOperationException">Thrown when all loaded keys for this context are marked as retired.</exception>
    public async Task<KeyRing<TContext>> LoadAsync(CancellationToken cancellationToken = default) {
        IReadOnlyList<EncryptionKeyRecord> records =
            await store.LoadKeysAsync(this._contextName, cancellationToken);

        if(records.Count == 0) {
            return await BootstrapAsync(cancellationToken);
        }

        SecurityMeter.KeyRingReloadCount.Add(1, SecurityMeter.ContextTag<TContext>());

        using MasterKey masterKey = await masterKeyProvider.GetMasterKeyAsync(cancellationToken);
        return BuildKeyRing(records, masterKey);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates, wraps, and persists the very first key (version 1) for this context.
    /// Handles concurrent multi-node initializations gracefully.
    /// </summary>
    private async Task<KeyRing<TContext>> BootstrapAsync(CancellationToken cancellationToken) {
        byte[] keyMaterial = new byte[this._options.KeySizeInBytes];
        try {
            RandomNumberGenerator.Fill(keyMaterial);

            using MasterKey masterKey = await masterKeyProvider.GetMasterKeyAsync(cancellationToken);
            string wrapped = masterKey.Wrap(keyMaterial);

            EncryptionKeyRecord record = new() {
                Id = Guid.CreateVersion7(),
                ContextName = this._contextName,
                Version = 1,
                WrappedKeyMaterial = wrapped,
                CreatedAt = timeProvider.GetUtcNow(),
            };

            try {
                await store.SaveKeyAsync(record, cancellationToken);
                SecurityMeter.KeyRingReloadCount.Add(1, SecurityMeter.ContextTag<TContext>());
                return BuildKeyRing([record], masterKey);
            }
            catch(Exception) {
                // Multi-node race condition fallback:
                // If another pod/instance successfully persisted Version 1 a millisecond earlier,
                // catch the unique constraint collision and reload the newly persisted keys.
                IReadOnlyList<EncryptionKeyRecord> existingRecords =
                    await store.LoadKeysAsync(this._contextName, cancellationToken);

                if(existingRecords.Count > 0) {
                    SecurityMeter.KeyRingReloadCount.Add(1, SecurityMeter.ContextTag<TContext>());
                    return BuildKeyRing(existingRecords, masterKey);
                }

                // If it was a genuine database failure (connection drop, etc.), rethrow
                throw;
            }
        }
        finally {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }

    /// <summary>
    /// Unwraps each persisted DEK record into secure unmanaged memory and constructs the <see cref="KeyRing{TContext}"/>.
    /// </summary>
    private static KeyRing<TContext> BuildKeyRing(
        IReadOnlyList<EncryptionKeyRecord> records,
        MasterKey masterKey) {

        // Active key is the highest version number that has not been retired
        EncryptionKeyRecord? active = records
            .Where(r => !r.IsRetired)
            .MaxBy(r => r.Version)
            ?? throw new InvalidOperationException(
                $"All keys for context '{typeof(TContext).Name}' are retired. " +
                "There must be at least one active key to build a valid key ring.");

        KeyRingBuilder<TContext> builder = new();

        foreach(EncryptionKeyRecord record in records) {
            try {
                EncryptionKey dek = masterKey.UnwrapToKey(
                    record.WrappedKeyMaterial,
                    KeyVersion.Of(record.Version),
                    record.IsRetired);

                if(record.IsRetired) {
                    builder.WithRetiredKey(dek);
                }
                else if(record.Version == active.Version) {
                    builder.WithCurrentKey(dek);
                }
            }
            catch(Exception) when(record.IsRetired) {
                // A corrupted historical/retired key must not prevent the application from loading active keys.
                // It is safely skipped.
            }
        }

        return builder.Build();
    }
}