using System.Collections.Concurrent;
using System.Text;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Testing;

/// <summary>
/// Thread-safe, in-memory, deterministic implementation of <see cref="IPersistentBloomFilter"/> for unit and integration testing.
/// Provides zero false-positive rate and inspection capabilities for test assertions.
/// </summary>
public class FakeBloomFilter : IPersistentBloomFilter {
    private readonly ConcurrentDictionary<string, bool> _items = new(StringComparer.Ordinal);
    private volatile bool _isDirty;

    /// <inheritdoc/>
    public FilterName Name { get; }

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

    /// <inheritdoc/>
    public virtual bool IsDirty => this._isDirty;

    /// <summary>
    /// Gets the number of times <see cref="SaveAsync"/> has been called.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Gets the number of times <see cref="ReloadAsync"/> has been called.
    /// </summary>
    public int ReloadCount { get; private set; }

    /// <summary>
    /// Gets all items that have been added to this filter as UTF-8 / string representations.
    /// </summary>
    public IReadOnlyCollection<string> AddedItems => [.. this._items.Keys];

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeBloomFilter"/> class with a default configuration.
    /// </summary>
    public FakeBloomFilter() : this("test-filter") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeBloomFilter"/> class with a default configuration.
    /// </summary>
    /// <param name="name">The name of the filter. Default is "test-filter".</param>
    public FakeBloomFilter(string name) : this(BloomFilterTestFactory.CreateConfiguration(name)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeBloomFilter"/> class with a specific configuration.
    /// </summary>
    /// <param name="configuration">The filter configuration.</param>
    public FakeBloomFilter(BloomFilterConfiguration configuration) {
        Preca.ThrowIfNull(configuration);
        this.Configuration = configuration;
        this.Name = configuration.Name;
    }

    /// <inheritdoc/>
    public virtual bool Add(ReadOnlySpan<byte> item) {
        string key = ConvertToKey(item);
        bool added = this._items.TryAdd(key, true);
        if(added) {
            this._isDirty = true;
        }
        return added;
    }

    /// <inheritdoc/>
    public virtual bool Contains(ReadOnlySpan<byte> item) {
        string key = ConvertToKey(item);
        return this._items.ContainsKey(key);
    }

    /// <inheritdoc/>
    public virtual bool Add(ReadOnlySpan<char> item) {
        string key = item.ToString();
        bool added = this._items.TryAdd(key, true);
        if(added) {
            this._isDirty = true;
        }
        return added;
    }

    /// <inheritdoc/>
    public virtual bool Contains(ReadOnlySpan<char> item) {
        string key = item.ToString();
        return this._items.ContainsKey(key);
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        return this._items.Count;
    }

    /// <inheritdoc/>
    public virtual ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._isDirty = false;
        this.SaveCount++;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        this._isDirty = false;
        this.ReloadCount++;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resets the filter by clearing all recorded items.
    /// </summary>
    public void Reset() {
        this._items.Clear();
        this._isDirty = false;
    }

    /// <summary>
    /// Helper to check if a specific string was added to the filter.
    /// </summary>
    public bool WasAdded(string item) {
        return this._items.ContainsKey(item);
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static string ConvertToKey(ReadOnlySpan<byte> bytes) {
        try {
            return StrictUtf8.GetString(bytes);
        }
        catch {
            return Convert.ToHexStringLower(bytes);
        }
    }
}