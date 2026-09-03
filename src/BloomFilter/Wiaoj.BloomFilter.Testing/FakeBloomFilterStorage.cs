using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Testing;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IBloomFilterStorage"/> for testing storage, backup, and reload flows.
/// </summary>
public sealed class FakeBloomFilterStorage : IBloomFilterStorage {
    private readonly ConcurrentDictionary<FilterName, (BloomFilterConfiguration Config, byte[] Data)> _storage = new();

    /// <inheritdoc/>
    public async Task<bool> SaveAsync(
        FilterName filterName,
        BloomFilterConfiguration config,
        Stream source,
        CancellationToken cancellationToken = default) {

        if(filterName.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(filterName));
        }

        Preca.ThrowIfNull(config, nameof(config));
        Preca.ThrowIfNull(source, nameof(source));

        using MemoryStream ms = new();
        await source.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        this._storage[filterName] = (config, ms.ToArray());
        return true;
    }

    /// <inheritdoc/>
    public ValueTask<(BloomFilterConfiguration? Config, Stream DataStream)?> LoadStreamAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default) {

        if(filterName.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(filterName));
        }

        if(this._storage.TryGetValue(filterName, out (BloomFilterConfiguration Config, byte[] Data) entry)) {
            Stream stream = new MemoryStream(entry.Data, writable: false);
            return ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>((entry.Config, stream));
        }

        return ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>(null);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default) {

        if(filterName.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(filterName));
        }

        this._storage.TryRemove(filterName, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a snapshot exists in storage for the given strongly-typed filter name.
    /// </summary>
    /// <param name="filterName">The filter name to query.</param>
    /// <returns><see langword="true"/> if the snapshot exists; otherwise, <see langword="false"/>.</returns>
    public bool Exists(FilterName filterName) {
        return this._storage.ContainsKey(filterName);
    }

    /// <summary>
    /// Clears all stored filter snapshots.
    /// </summary>
    public void Clear() {
        this._storage.Clear();
    }
}