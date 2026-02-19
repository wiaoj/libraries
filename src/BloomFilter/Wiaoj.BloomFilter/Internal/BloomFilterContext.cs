using Microsoft.Extensions.Logging;
using Wiaoj.ObjectPool;
using Wiaoj.Serialization;

namespace Wiaoj.BloomFilter.Internal; 
public sealed record class BloomFilterContext(
    IBloomFilterStorage? Storage,
    IObjectPool<MemoryStream> MemoryStreamPool,
    ISerializer<InMemorySerializerKey> Serializer,
    ILogger Logger,
    BloomFilterOptions Options,
    TimeProvider TimeProvider
);