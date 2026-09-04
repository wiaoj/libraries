using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents the immutable configuration parameters for a Bloom Filter.
/// This is a pure data record without any calculation logic.
/// </summary>
public sealed record BloomFilterConfiguration {
    /// <summary>
    /// The exclusive minimum allowed error rate (0.0).
    /// </summary>
    public const double MinimumErrorRate = 0.0;

    /// <summary>
    /// The exclusive maximum allowed error rate (1.0).
    /// </summary>
    public const double MaximumErrorRate = 1.0;

    /// <summary>
    /// The default 64-bit seed constant (0x7769616F6A5F6266 = "wiaoj_bf") used for hash generation.
    /// </summary>
    public const long DefaultHashSeed = 0x7769616F6A5F6266L;

    /// <summary>
    /// Gets the unique name of the filter.
    /// </summary>
    public FilterName Name { get; init; }

    /// <summary>
    /// Gets the expected number of elements (n) to be inserted into the filter.
    /// </summary>
    public long ExpectedItems { get; init; }

    /// <summary>
    /// Gets the desired false positive probability (p).
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Gets the number of shards for the filter.
    /// </summary>
    public int ShardCount { get; init; }

    /// <summary>
    /// Gets the hash seed used for bit calculation.
    /// </summary>
    public long HashSeed { get; init; }

    /// <summary>
    /// Gets the size of the bit array (m) in bits.
    /// </summary>
    public long SizeInBits { get; init; }

    /// <summary>
    /// Gets the number of hash functions (k).
    /// </summary>
    public int HashFunctionCount { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilterConfiguration"/> record.
    /// </summary>
    /// <param name="name">The name of the filter.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target error rate.</param>
    /// <param name="sizeInBits">The total size in bits.</param>
    /// <param name="hashFunctionCount">The number of hash functions.</param>
    /// <param name="hashSeed">The seed for hashing.</param>
    /// <param name="shardCount">The number of shards.</param>
    public BloomFilterConfiguration(
        FilterName name,
        long expectedItems,
        double errorRate,
        long sizeInBits,
        int hashFunctionCount,
        long hashSeed,
        int shardCount = 1) {

        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, MinimumErrorRate, MaximumErrorRate);
        Preca.ThrowIfNegativeOrZero(sizeInBits);
        Preca.ThrowIfNegativeOrZero(hashFunctionCount);
        Preca.ThrowIfLessThan(shardCount, 1);

        this.Name = name;
        this.ExpectedItems = expectedItems;
        this.ErrorRate = errorRate;
        this.SizeInBits = sizeInBits;
        this.HashFunctionCount = hashFunctionCount;
        this.HashSeed = hashSeed;
        this.ShardCount = shardCount;
    }

    /// <summary>
    /// Creates a new configuration with the specified shard count.
    /// </summary>
    /// <param name="count">The new shard count. Must be greater than or equal to 1.</param>
    /// <returns>A new configuration instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1.</exception>
    public BloomFilterConfiguration WithShardCount(int count) {
        Preca.ThrowIfLessThan(count, 1, () => new ArgumentOutOfRangeException(nameof(count), "Shard count must be greater than or equal to 1."));
        return this with { ShardCount = count };
    }

    /// <summary>
    /// Creates a new configuration with the specified hash seed.
    /// </summary>
    /// <param name="seed">The new hash seed.</param>
    /// <returns>A new configuration instance.</returns>
    public BloomFilterConfiguration WithHashSeed(long seed) {
        return this with { HashSeed = seed };
    }
}