using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Options;

namespace Wiaoj.BloomFilter.Redis.Engine;

/// <summary>
/// Strongly-typed marker wrapper for <see cref="SynchronizedRedisBloomFilter"/> allowing Dependency Injection to distinguish between different filters.
/// </summary>
/// <typeparam name="TTag">The marker type identifying the filter domain context.</typeparam>
public sealed class SynchronizedRedisBloomFilter<TTag> : SynchronizedRedisBloomFilter, IBloomFilter<TTag>, IAsyncBloomFilter<TTag> where TTag : notnull {


    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronizedRedisBloomFilter{TTag}"/> class with internal in-memory filter.
    /// </summary>
    internal SynchronizedRedisBloomFilter(
        IConnectionMultiplexer redis,
        InMemoryBloomFilter innerFilter,
        IOptions<SynchronizedBloomFilterOptions> options)
        : base(redis, innerFilter, options) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronizedRedisBloomFilter{TTag}"/> class with internal in-memory filter and custom logger.
    /// </summary>
    internal SynchronizedRedisBloomFilter(
        IConnectionMultiplexer redis,
        InMemoryBloomFilter innerFilter,
        IOptions<SynchronizedBloomFilterOptions> options,
        ILogger<SynchronizedRedisBloomFilter> logger)
        : base(redis, innerFilter, options, logger) {
    }
}
