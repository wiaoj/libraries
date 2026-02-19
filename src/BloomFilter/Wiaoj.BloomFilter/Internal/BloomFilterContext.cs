using Microsoft.Extensions.Logging;
using Wiaoj.ObjectPool;

namespace Wiaoj.BloomFilter.Internal; 
public sealed record class BloomFilterContext(
    IBloomFilterStorage? Storage,
    IObjectPool<MemoryStream> MemoryStreamPool,
    ILogger Logger,
    BloomFilterOptions Options,
    TimeProvider TimeProvider
);