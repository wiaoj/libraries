using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Testing;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IBloomFilterStorage"/> for testing storage, backup, and reload flows.
/// </summary>
public sealed class InMemoryBloomFilterStorage : IBloomFilterStorage {
    private readonly ConcurrentDictionary<string, (BloomFilterConfiguration Config, byte[] Data)> _storage = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask SaveAsync(string filterName, BloomFilterConfiguration config, Stream source, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(filterName);
        Preca.ThrowIfNull(config);
        Preca.ThrowIfNull(source);

        using MemoryStream ms = new();
        await source.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        this._storage[filterName] = (config, ms.ToArray());
    }

    /// <inheritdoc/>
    public ValueTask<(BloomFilterConfiguration Config, Stream DataStream)?> LoadStreamAsync(string filterName, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(filterName);

        if(this._storage.TryGetValue(filterName, out var entry)) {
            Stream stream = new MemoryStream(entry.Data, writable: false);
            return ValueTask.FromResult<(BloomFilterConfiguration Config, Stream DataStream)?>((entry.Config, stream));
        }

        return ValueTask.FromResult<(BloomFilterConfiguration Config, Stream DataStream)?>(null);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string filterName, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(filterName);
        this._storage.TryRemove(filterName, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all stored filter snapshots.
    /// </summary>
    public void Clear() => this._storage.Clear();

    /// <summary>
    /// Checks if a snapshot exists in storage for the given filter name.
    /// </summary>
    public bool Exists(string filterName) => this._storage.ContainsKey(filterName);
}