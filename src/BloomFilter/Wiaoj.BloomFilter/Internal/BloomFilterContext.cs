using Microsoft.Extensions.Logging;
using Wiaoj.ObjectPool;

namespace Wiaoj.BloomFilter.Internal; 

/// <summary>
/// Provides contextual dependencies and configuration required for initializing Bloom Filter instances.
/// </summary>
/// <param name="Storage">The storage provider for persistence.</param>
/// <param name="MemoryStreamPool">The pool for reusing memory streams.</param>
/// <param name="Logger">The logger instance for the filter.</param>
/// <param name="Options">Global Bloom Filter options.</param>
/// <param name="TimeProvider">The provider for time-based operations.</param>
/// <param name="ConfigFactory">The factory for creating filter configurations.</param>
public sealed record class BloomFilterContext(
    IBloomFilterStorage? Storage,
    IObjectPool<MemoryStream> MemoryStreamPool,
    ILogger Logger,
    BloomFilterOptions Options,
    TimeProvider TimeProvider,
    IBloomFilterConfigurationFactory ConfigFactory
);