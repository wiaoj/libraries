# Wiaoj.BloomFilter

High-performance, zero-allocation, thread-safe probabilistic filter library for .NET supporting Single, Auto-Sharded, Scalable (layered), and Rotating (sliding-window) architectures with persistence, compression, and observability.

---

## Packages

| Package | Description |
| :--- | :--- |
| **`Wiaoj.BloomFilter.Abstractions`** | Core contracts, interfaces (`IBloomFilter`, `IPersistentBloomFilter`, `IBloomFilterStorage`), configuration models (`BloomFilterConfiguration`), and value primitives (`FilterName`, `GrowthRate`, `Percentage`). Zero external dependencies. |
| **`Wiaoj.BloomFilter`** | Core high-throughput SIMD-accelerated engine (`InMemoryBloomFilter`, `ShardedBloomFilter`, `ScalableBloomFilter`, `RotatingBloomFilter`), persistent storage provider (`FileSystemBloomFilterStorage`), background lifecycle workers, and Microsoft DI integration. |
| **`Wiaoj.BloomFilter.Testing`** | Test doubles (`FakeBloomFilter`, `FakeBloomFilterStorage`) for unit and integration testing without filesystem or background thread dependencies. |

---

## Architecture Overview

```
                      ┌───────────────────────────────────────────────┐
                      │        Wiaoj.BloomFilter.Abstractions         │
                      │  (Contracts, Primitives, Options & Structs)   │
                      └───────────────────────┬───────────────────────┘
                                              │
                     ┌────────────────────────┴────────────────────────┐
                     │                                                 │
                     ▼                                                 ▼
     ┌───────────────────────────────┐                 ┌───────────────────────────────┐
     │       Wiaoj.BloomFilter       │                 │   Wiaoj.BloomFilter.Testing   │
     │  (Core SIMD Engine, Sharded,  │                 │    (FakeBloomFilter, Fake     │
     │   Scalable, Rotating, Storage)│                 │     Storage & Test Harness)   │
     └───────────────┬───────────────┘                 └───────────────────────────────┘
                     │
                     ▼
     ┌───────────────────────────────┐
     │      Hosting & Lifecycle      │
     │   (AutoSave, WarmUp, Seeding) │
     └───────────────────────────────┘
```

---

## Architectural Variants

| Variant | Implementation | Description | Best For |
| :--- | :--- | :--- | :--- |
| **InMemory** | `InMemoryBloomFilter` | Single bit array backed by `ArrayPool<ulong>.Shared`. SIMD-accelerated (AVX2 Vector256 / SSE2 Vector128) bit checking. | Standard fixed-capacity filtering with sub-microsecond latency. |
| **Sharded** | `ShardedBloomFilter` | Partitions capacity across $2^N$ internal shards using base hash routing to prevent Large Object Heap (LOH) allocations. | Filters with tens or hundreds of millions of items ($> 100 \text{ MB}$). |
| **Scalable** | `ScalableBloomFilter` | Dynamically appends new layers when saturation threshold is reached. Applies geometric error tightening ($r = 0.85$, Almeida et al., 2007) to guarantee bounded cumulative false positive rate. | Workloads with unpredictable or unbounded growth over time. |
| **Rotating** | `RotatingBloomFilter` | Sliding-window filter with TTL expiration. Rotates time-stamped shards and evicts expired windows automatically. | Sliding deduplication windows (e.g. "seen in the last 24 hours"). |

---

## Hashing & Mathematical Engine

- **Kirsch-Mitzenmacher Double Hashing:** Derives $k$ distinct hash values from a single 128-bit hash execution:
  $$g_i(x) = h_1(x) + i \cdot h_2(x)$$
- **Lemire's Fast Range Reduction:** Maps 64-bit hash values to bit array indices without costly integer modulo division:
  $$\text{pos} = \lfloor \frac{\text{combinedHash} \times m}{2^{64}} \rfloor$$
- **Degeneracy Protection:** When $h_2 = 0$, the engine automatically falls back to Knuth's Golden Ratio constant (`0x9E3779B97F4A7C15UL` - $2^{64} / \phi$) to maximize bit dispersion and prevent 1-hash collapse.
- **Hardware Population Count:** PopCount queries execute via CPU vector instructions (`BitOperations.PopCount`) across 64-bit words.

---

## Quick Start

### 1. Installation

```bash
dotnet add package Wiaoj.BloomFilter
```

### 2. Dependency Injection Setup

```csharp
using Wiaoj.BloomFilter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBloomFilter(builder => {
    // Storage configuration for snapshots via fluent extension
    builder.UseFileSystemStorage(storage => {
        storage.Path = "BloomData";
        storage.EnableCompression = true;
    });

    // Background lifecycle services
    builder.AddAutoSave(); // Periodic snapshotting
    builder.AddWarmUp();   // Preload filters at startup

    // 1. Fixed capacity in-memory filter
    builder.AddFilter<UserBlacklistTag>("user-blacklist", expectedItems: 500_000, errorRate: 0.01);

    // 2. Scalable dynamically-layered filter
    builder.AddScalableFilter<PaymentDeduplicationTag>(
        "payment-dedup",
        initialCapacity: 100_000,
        errorRate: 0.001,
        growthRate: 2.0,
        saturationThreshold: 0.50);

    // 3. Sliding-window rotating filter
    builder.AddRotatingFilter<IpRateLimitTag>(
        "ip-rate-limit",
        capacity: 1_000_000,
        errorRate: 0.01,
        windowSize: TimeSpan.FromHours(24),
        shardCount: 24);
});
```

### 3. Usage via Strongly-Typed Marker Tags

```csharp
public sealed record UserBlacklistTag;

public sealed class UserService(IBloomFilter<UserBlacklistTag> blacklistFilter) {
    public bool IsUserBlacklisted(ReadOnlySpan<char> username) {
        return blacklistFilter.Contains(username);
    }

    public void BlacklistUser(ReadOnlySpan<char> username) {
        blacklistFilter.Add(username);
    }
}
```

### 4. Direct Injection via Registry or Factory

```csharp
public sealed class SecurityService(IBloomFilterRegistry registry) {
    public bool CheckIp(string ipAddress) {
        if (registry.TryGet("ip-rate-limit", out IPersistentBloomFilter? filter)) {
            return filter.Contains(ipAddress);
        }
        return false;
    }
}
```

---

## Observability & Diagnostics

`Wiaoj.BloomFilter` includes native OpenTelemetry diagnostics:

- **ActivitySource:** `"Wiaoj.BloomFilter"` traces for `Save`, `Reload`, and `ScaleUp` operations.
- **Meters:**
  - `bloomfilter.lookups.count`: Total lookup operations.
  - `bloomfilter.hits.count`: Positive lookup hits.
  - `bloomfilter.save.duration`: Time spent writing snapshots to persistent storage.
  - `bloomfilter.reload.duration`: Time spent recovering snapshots from storage.
  - `bloomfilter.scalable.layers.spawned`: Counter of dynamically spawned scaling layers.

---

## License

This project is licensed under the MIT License.
