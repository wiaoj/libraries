using Microsoft.Extensions.Logging;
using Microsoft.IO; 

namespace Wiaoj.BloomFilter.Engine;

/// <summary>
/// Provides contextual dependencies and configuration required for initializing Bloom Filter instances.
/// </summary>
/// <param name="Storage">The storage provider for persistence.</param>
/// <param name="RecyclableMemoryStreamManager">The pool for reusing memory streams.</param>
/// <param name="Logger">The logger instance for the filter.</param>
/// <param name="Options">Global Bloom Filter options.</param>
/// <param name="TimeProvider">The provider for time-based operations.</param>
/// <param name="ConfigFactory">The factory for creating filter configurations.</param>
internal sealed record class BloomFilterContext(
    IBloomFilterStorage? Storage,
    RecyclableMemoryStreamManager RecyclableMemoryStreamManager,
    ILogger Logger,
    BloomFilterOptions Options,
    TimeProvider TimeProvider,
    IBloomFilterConfigurationFactory ConfigFactory) {

    /// <summary>
    /// Creates a leaf persistent filter (either Sharded or InMemory depending on size threshold)
    /// using the current execution context.
    /// </summary>
    public IPersistentBloomFilter CreateLeafFilter(BloomFilterConfiguration config) {
        int shards = BloomMath.CalculateOptimalShardCount(config.SizeInBits, this.Options.Lifecycle.ShardingThresholdBytes);

        return shards > 1
            ? new ShardedBloomFilter(config.WithShardCount(shards), this)
            : new InMemoryBloomFilter(config, this);
    }

    /// <summary>
    /// Creates an explicitly partitioned ShardedBloomFilter using the specified shard count,
    /// or calculates the optimal shard count based on ShardingThresholdBytes if shardCount is not specified.
    /// </summary>
    public IPersistentBloomFilter CreateShardedFilter(BloomFilterConfiguration config, int shardCount = 0) {
        int shards = shardCount >= 2
            ? shardCount
            : Math.Max(2, BloomMath.CalculateOptimalShardCount(config.SizeInBits, this.Options.Lifecycle.ShardingThresholdBytes));

        return new ShardedBloomFilter(config.WithShardCount(shards), this);
    }
}