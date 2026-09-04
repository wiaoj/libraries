using Microsoft.Extensions.Logging;
using Wiaoj.ObjectPool;

namespace Wiaoj.BloomFilter.Engine;

/// <summary>
/// Provides contextual dependencies and configuration required for initializing Bloom Filter instances.
/// </summary>
/// <param name="Storage">The storage provider for persistence.</param>
/// <param name="MemoryStreamPool">The pool for reusing memory streams.</param>
/// <param name="Logger">The logger instance for the filter.</param>
/// <param name="Options">Global Bloom Filter options.</param>
/// <param name="TimeProvider">The provider for time-based operations.</param>
/// <param name="ConfigFactory">The factory for creating filter configurations.</param>
internal sealed record class BloomFilterContext(
    IBloomFilterStorage? Storage,
    IObjectPool<MemoryStream> MemoryStreamPool,
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
}