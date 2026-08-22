using System.Collections.Concurrent;
using System.Text;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Testing;

/// <summary>
/// Thread-safe, in-memory, deterministic implementation of <see cref="IBloomFilter"/> for unit and integration testing.
/// Provides zero false-positive rate and inspection capabilities for test assertions.
/// </summary>
public class FakeBloomFilter : IBloomFilter {
    private readonly ConcurrentDictionary<string, bool> _items = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

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
        this.Name = configuration.Name.Value;
    }

    /// <inheritdoc/>
    public virtual bool Add(ReadOnlySpan<byte> item) {
        string key = ConvertToKey(item);
        return this._items.TryAdd(key, true);
    }

    /// <inheritdoc/>
    public virtual bool Contains(ReadOnlySpan<byte> item) {
        string key = ConvertToKey(item);
        return this._items.ContainsKey(key);
    }

    /// <inheritdoc/>
    public virtual bool Add(ReadOnlySpan<char> item) {
        string key = item.ToString();
        return this._items.TryAdd(key, true);
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

    /// <summary>
    /// Resets the filter by clearing all recorded items.
    /// </summary>
    public void Reset() {
        this._items.Clear();
    }

    /// <summary>
    /// Helper to check if a specific string was added to the filter.
    /// </summary>
    public bool WasAdded(string item) {
        return this._items.ContainsKey(item);
    }

    private static string ConvertToKey(ReadOnlySpan<byte> bytes) {
        // UTF-8 olarak okumayı dener; geçersizse hex string'e çevirir (tam binary uyumluluğu)
        try {
            return Encoding.UTF8.GetString(bytes);
        }
        catch {
            return Convert.ToHexStringLower(bytes);
        }
    }
}