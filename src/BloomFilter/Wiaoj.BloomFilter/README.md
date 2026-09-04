# Wiaoj.BloomFilter

Production-grade, high-throughput Bloom Filter engine for .NET.

Features hardware SIMD acceleration (AVX2 Vector256 / SSE2 Vector128), lock-free read paths, LOH-safe sharding, scalable layered growth, sliding-window rotation, atomic filesystem persistence, and background lifecycle workers.

---

## Installation

```bash
dotnet add package Wiaoj.BloomFilter
```

---

## Architectural Engines

### 1. `InMemoryBloomFilter`
- High-speed single bit array renting 64-bit unsigned integer buffers from `ArrayPool<ulong>.Shared`.
- **Vectorized Bit Checking:** Uses `Vector256<ulong>` (AVX2) and `Vector128<ulong>` (SSE2) to evaluate multiple hash iterations simultaneously.
- **Snapshot Integrity:** Writes 32-byte header containing checksum, fingerprint, and size to ensure snapshot compatibility.

### 2. `ShardedBloomFilter`
- Partitions large capacities into $2^N$ independent `InMemoryBloomFilter` shards.
- Single-pass 128-bit hash execution: upper bits route to the shard index while lower bits evaluate the Kirsch-Mitzenmacher sequence, eliminating double-hashing overhead.
- Eliminates Large Object Heap (LOH) fragmentation for filters with millions of items.

### 3. `ScalableBloomFilter`
- Automatically scales capacity by spawning new leaf layers when the current layer's fill ratio reaches the saturation threshold.
- **Geometric Tightening:** Each subsequent layer is spawned with a tightened error rate ($p_i = p_{i-1} \cdot 0.85$, Almeida et al., 2007) ensuring the cumulative false positive probability across all layers remains within the initial target.

### 4. `RotatingBloomFilter`
- Time-to-Live (TTL) sliding-window filter with pre-allocated sliding shards.
- Uses `TryEnterWriteLock(0)` to prevent lock convoys and thundering herd spikes during window transitions.
- Queries span all active time windows; expired windows are evicted automatically.

---

## Storage & Persistence

### `FileSystemBloomFilterStorage`
- Persists snapshots to local disk using atomic file replacements (`File.Move` with temporary files).
- Supports transparent GZip compression (`storage.EnableCompression = true`).
- Configured via `builder.UseFileSystemStorage(...)` extension overloads.
- Fail-fast validation prevents data corruption on non-seekable streams.

---

## Background Lifecycle Services

- **`BloomFilterAutoSaveService`**: Periodically persists dirty filters to storage using background timers.
- **`BloomFilterWarmUpService`**: Preloads and initializes filters during application startup.
- **`BloomFilterSeedingService`**: Hydrates empty filters on first boot from registered `IAutoBloomFilterSeeder` instances without re-running on legitimately empty tables.

---

## Registration Example

```csharp
builder.Services.AddBloomFilter(builder => {
    // Persistent storage configuration
    builder.UseFileSystemStorage(storage => {
        storage.Path = "BloomData";
        storage.EnableCompression = true;
    });

    // Lifecycle services
    builder.AddAutoSave();
    builder.AddWarmUp();
    builder.AddAutoReseed();

    // 1. InMemory filter
    builder.AddFilter<UserSessionTag>("sessions", 1_000_000, 0.01);

    // 2. Sharded filter (LOH-safe, power-of-two partitions)
    builder.AddShardedFilter<CatalogTag>("catalog", expectedItems: 5_000_000, errorRate: 0.01, shardCount: 8);

    // 3. Scalable filter (dynamic layered growth)
    builder.AddScalableFilter<TransactionTag>("transactions", initialCapacity: 100_000, errorRate: 0.001);

    // 4. Rotating filter (time-windowed sliding expiration)
    builder.AddRotatingFilter<RateLimitTag>("rate-limits", capacity: 500_000, errorRate: 0.01, windowSize: TimeSpan.FromHours(1), shardCount: 6);
});
```

---

## License

This project is licensed under the MIT License.
