using System.Text;
using Wiaoj.Abstractions;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents the immutable configuration parameters for a Bloom Filter.
/// Calculates the optimal bit size and hash function count based on desired capacity and error rate.
/// </summary>
public sealed record BloomFilterConfiguration : IShallowCloneable<BloomFilterConfiguration> {
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
    public Percentage ErrorRate { get; init; }
    
    public int ShardCount { get; private set; }
  
    public long HashSeed { get; init; }

    /// <summary>
    /// Gets the calculated optimal size of the bit array (m) in bits.
    /// </summary>
    public long SizeInBits { get; }

    /// <summary>
    /// Gets the calculated optimal number of hash functions (k).
    /// </summary>
    public int HashFunctionCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilterConfiguration"/> class.
    /// Automatically calculates optimal <see cref="SizeInBits"/> and <see cref="HashFunctionCount"/>.
    /// </summary>
    /// <param name="name">The name of the filter.</param>
    /// <param name="expectedItems">The expected number of elements to store.</param>
    /// <param name="errorRate">The acceptable false positive rate (e.g., 0.01 for 1%).</param>
    /// <exception cref="ArgumentNullException">Thrown if name is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if expected items is not positive.</exception>
    public BloomFilterConfiguration(FilterName name,
                                    long expectedItems,
                                    Percentage errorRate) : this(name, expectedItems,errorRate, 0x7769616F6A5F6266) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilterConfiguration"/> class.
    /// Automatically calculates optimal <see cref="SizeInBits"/> and <see cref="HashFunctionCount"/>.
    /// </summary>
    /// <param name="name">The name of the filter.</param>
    /// <param name="expectedItems">The expected number of elements to store.</param>
    /// <param name="errorRate">The acceptable false positive rate (e.g., 0.01 for 1%).</param>
    /// <exception cref="ArgumentNullException">Thrown if name is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if expected items is not positive.</exception>
    public BloomFilterConfiguration(FilterName name, long expectedItems, Percentage errorRate, long hashSeed) {
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfOutOfRange(
           errorRate.Value,
           minimum: Percentage.Zero,
           maximum: Percentage.Full,
           () => new ArgumentException("The false positive error rate must be a value between 0 and 1 (exclusive). Found: " + errorRate.Value));

        this.Name = name;
        this.ExpectedItems = expectedItems;
        this.ErrorRate = errorRate;
        this.HashSeed = hashSeed;

        // Formula for optimal bit size (m): m = -(n * ln(p)) / (ln(2)^2)
        double m = -(expectedItems * Math.Log(errorRate.Value)) / Math.Pow(Math.Log(2), 2);
        this.SizeInBits = (long)Math.Ceiling(m);

        // Formula for optimal hash functions (k): k = (m / n) * ln(2)
        double k = (m / expectedItems) * Math.Log(2);
        this.HashFunctionCount = (int)Math.Ceiling(k);
    }

    public BloomFilterConfiguration WithShardCount(int count) {
        return this with { ShardCount = count };
    } 

    public BloomFilterConfiguration ShallowClone() {
        return this with {  };
    }
}