using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wiaoj.BloomFilter.Redis.Options;

namespace Wiaoj.BloomFilter.Redis.Engine;

/// <summary>
/// Strongly-typed marker wrapper for <see cref="DistributedRedisBloomFilter"/> allowing Dependency Injection to distinguish between different filters.
/// </summary>
/// <typeparam name="TTag">The marker type identifying the filter domain context.</typeparam>
public sealed class DistributedRedisBloomFilter<TTag> : DistributedRedisBloomFilter, IBloomFilter<TTag>, IAsyncBloomFilter<TTag> where TTag : notnull {
    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRedisBloomFilter{TTag}"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="configuration">The immutable filter configuration parameters.</param>
    /// <param name="options">The distributed filter options.</param>
    public DistributedRedisBloomFilter(
        IConnectionMultiplexer redis,
        BloomFilterConfiguration configuration,
        IOptions<DistributedBloomFilterOptions> options)
        : base(redis, configuration, options) {
    }
}
