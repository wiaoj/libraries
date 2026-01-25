namespace Wiaoj.BloomFilter;
/// <summary>
/// Defines the core operations of a probabilistic Bloom Filter data structure.
/// </summary>
public interface IBloomFilter {
    /// <summary>
    /// Gets the unique name identifier of the filter.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the configuration parameters (capacity, error rate, hash functions) of the filter.
    /// </summary>
    BloomFilterConfiguration Configuration { get; }

    /// <summary>
    /// Adds an item (represented as a byte span) to the set.
    /// </summary>
    /// <param name="item">The binary representation of the item to add.</param>
    /// <returns>
    /// <c>true</c> if at least one bit in the filter was changed from 0 to 1; 
    /// <c>false</c> if all bits for this item were already set (indicating the item might already exist).
    /// </returns>
    bool Add(ReadOnlySpan<byte> item);

    /// <summary>
    /// Tests whether an item is present in the set.
    /// </summary>
    /// <param name="item">The binary representation of the item to check.</param>
    /// <returns>
    /// <c>true</c> if the item <b>might</b> contain the element (False positives are possible).
    /// <c>false</c> if the set <b>definitely does not</b> contain the element.
    /// </returns>
    bool Contains(ReadOnlySpan<byte> item);

    // --- String Overloads ---

    /// <summary>
    /// Adds a string item to the set using UTF-8 encoding.
    /// </summary>
    /// <param name="item">The string item to add.</param>
    /// <returns>
    /// <c>true</c> if the internal state changed; otherwise, <c>false</c>.
    /// </returns>
    bool Add(ReadOnlySpan<char> item);

    /// <summary>
    /// Tests whether a string item is present in the set using UTF-8 encoding.
    /// </summary>
    /// <param name="item">The string item to check.</param>
    /// <returns>
    /// <c>true</c> if the item might be present; <c>false</c> if it is definitely not present.
    /// </returns>
    bool Contains(ReadOnlySpan<char> item);

    /// <summary>
    /// Returns the total number of bits currently set to 1.
    /// Used to calculate the fill ratio and estimate false positive probability.
    /// </summary>
    long GetPopCount();
}

/// <summary>
/// A marker interface representing a strongly-typed Bloom Filter for a specific domain entity or category.
/// Allows Dependency Injection to distinguish between different filters (e.g., <c>IBloomFilter&lt;UserTag&gt;</c> vs <c>IBloomFilter&lt;ProductTag&gt;</c>).
/// </summary>
/// <typeparam name="TTag">The marker type used to identify the filter context.</typeparam>
public interface IBloomFilter<TTag> : IBloomFilter where TTag : notnull;