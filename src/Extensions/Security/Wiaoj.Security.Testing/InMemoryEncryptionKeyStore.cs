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
        var query = _storage.Values
            .Where(x => x.ContextName == contextName)
            .OrderByDescending(x => x.Version);
           
        IReadOnlyList<EncryptionKeyRecord> list = query.OrderBy(x => x.Version).ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<EncryptionKeyRecord?> GetKeyAsync(string contextName, int version, CancellationToken ct = default) {
        _storage.TryGetValue((contextName, version), out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<EncryptionKeyRecord> SaveKeyAsync(EncryptionKeyRecord record, CancellationToken ct = default) {
        if (!_storage.TryAdd((record.ContextName, record.Version), record)) {
            throw new InvalidOperationException($"Key with version {record.Version} already exists for context {record.ContextName}.");
        }
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task RetireKeyAsync(string contextName, int version, CancellationToken ct = default) {
        if (_storage.TryGetValue((contextName, version), out var record)) {
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
    public void Clear() => _storage.Clear();
}
