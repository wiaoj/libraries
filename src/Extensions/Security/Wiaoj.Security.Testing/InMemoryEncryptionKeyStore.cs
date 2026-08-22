using System.Collections.Concurrent;

namespace Wiaoj.Security.Testing;

/// <summary>
/// A thread-safe, in-memory implementation of <see cref="IEncryptionKeyStore"/>.
/// Useful for testing key rotation and persistence logic without a database.
/// </summary>
public sealed class InMemoryEncryptionKeyStore : IEncryptionKeyStore {
    private readonly ConcurrentDictionary<(string Context, int Version), EncryptionKeyRecord> _storage = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<EncryptionKeyRecord>> LoadKeysAsync(string contextName, CancellationToken ct = default) {
        IOrderedEnumerable<EncryptionKeyRecord> query = this._storage.Values
            .Where(x => x.ContextName == contextName)
            .OrderBy(x => x.Version);

        IReadOnlyList<EncryptionKeyRecord> list = query.ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<EncryptionKeyRecord?> GetKeyAsync(string contextName, int version, CancellationToken ct = default) {
        this._storage.TryGetValue((contextName, version), out EncryptionKeyRecord? record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<EncryptionKeyRecord> SaveKeyAsync(EncryptionKeyRecord record, CancellationToken ct = default) {
        if(!this._storage.TryAdd((record.ContextName, record.Version), record)) {
            throw new InvalidOperationException($"Key with version {record.Version} already exists for context {record.ContextName}.");
        }
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task UpdateWrappedKeyAsync(string contextName, int version, string newWrappedKeyMaterial, CancellationToken ct = default) {
        if(this._storage.TryGetValue((contextName, version), out EncryptionKeyRecord? record)) {
            record.WrappedKeyMaterial = newWrappedKeyMaterial;
            return Task.CompletedTask;
        }

        throw new KeyNotFoundException($"Key {version} not found for context {contextName}.");
    }

    /// <inheritdoc />
    public Task RetireKeyAsync(string contextName, int version, CancellationToken ct = default) {
        if(this._storage.TryGetValue((contextName, version), out EncryptionKeyRecord? record)) {
            record.RetiredAt = DateTimeOffset.UtcNow;
        }
        else {
            throw new KeyNotFoundException($"Key {version} not found for context {contextName}.");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all stored keys.
    /// </summary>
    public void Clear() {
        this._storage.Clear();
    }
}