# Wiaoj.BloomFilter.Redis

High-performance Redis integration for `Wiaoj.BloomFilter` providing persistent snapshot storage, distributed remote bit-level filters, and hybrid memory-speed replicated filters with Redis Pub/Sub delta synchronization.

---

## 🚀 Operational Models

`Wiaoj.BloomFilter.Redis` provides **3 distinct operational models**, allowing systems to select the exact balance between latency, memory footprint, and cross-node consistency:

```
────────────────────────────────────────────────────────────────────────────────────────
Model 1: Snapshot Storage            Model 2: Live Distributed Filter     Model 3: Hybrid Replicated Filter
(Periodic / Startup Hydration)       (Zero L1 Memory, Single RTT Bits)    (L1 SIMD Speed + Pub/Sub Sync)
────────────────────────────────────────────────────────────────────────────────────────

 ┌─────────────────────────┐          ┌─────────────────────────┐          ┌─────────────────────────┐
 │   Node A (In-Memory)    │          │    Node A (Stateless)   │          │   Node A (L1 Memory)    │
 │  ┌───────────────────┐  │          │  ┌───────────────────┐  │          │  ┌───────────────────┐  │
 │  │ SIMD BitArray     │  │          │  │ Client-Side Hash  │  │          │  │ SIMD BitArray     │  │
 │  └─────────┬─────────┘  │          │  └─────────┬─────────┘  │          │  └─────────┬─────────┘  │
 └────────────┼────────────┘          └────────────┼────────────┘          └────────────┼────────────┘
              │ (Save / WarmUp)                    │ (Batch SETBIT/GETBIT)              │ (Pub/Sub Broadcast)
              ▼                                    ▼                                    ▼
 ┌─────────────────────────┐          ┌─────────────────────────┐          ┌─────────────────────────┐
 │       Redis Server      │          │       Redis Server      │          │       Redis Server      │
 │  [bloom:snapshot:users] │          │     [bloom:live:users]  │          │      (Redis Pub/Sub)    │
 │  (Serialized Snapshot)  │          │    (Remote Redis String)│          │  (h1, h2 Delta Broadcast│
 └─────────────────────────┘          └─────────────────────────┘          └────────────┬────────────┘
              ▲                                                                         │ (Subscribe Delta)
              │ (Save / WarmUp)                                                         ▼
 ┌────────────┴────────────┐                                               ┌─────────────────────────┐
 │   Node B (In-Memory)    │                                               │   Node B (L1 Memory)    │
 │  ┌───────────────────┐  │                                               │  ┌───────────────────┐  │
 │  │ SIMD BitArray     │  │                                               │  │ SIMD BitArray     │  │
 │  └───────────────────┘  │                                               │  └───────────────────┘  │
 └─────────────────────────┘                                               └─────────────────────────┘
```

### Architecture Comparison Matrix

| Feature / Metric | Model 1: Snapshot Storage | Model 2: Live Distributed Filter | Model 3: Hybrid Replicated Filter |
| :--- | :--- | :--- | :--- |
| **Primary Class** | `RedisBloomFilterStorage` | `DistributedRedisBloomFilter` | `SynchronizedRedisBloomFilter` |
| **Interface Implemented** | `IBloomFilterStorage` | `IBloomFilter`, `IAsyncBloomFilter` | `IBloomFilter`, `IAsyncBloomFilter`, `IPersistentBloomFilter` |
| **Read Latency** | **10–50 ns** (Local AVX2 SIMD) | **0.5–2 ms** (Redis TCP RTT) | **10–50 ns** (Local AVX2 SIMD) |
| **Write Latency** | **10–50 ns** (Local memory flush) | **0.5–2 ms** (Redis TCP RTT) | **10–50 ns** (Local SIMD + Async Pub/Sub) |
| **Per-Node Memory** | Full (local `PooledBitArray`) | **~0 MB** (stateless) | Full (local `PooledBitArray`) |
| **Cross-Node Consistency** | Eventual (periodic snapshot) | **Immediate / Strong** | **Near Real-time** (< 1ms via Pub/Sub) |
| **Network Overhead** | Low (periodic compressed dump) | Moderate (1 pipelined RTT per op) | Extremely low (32-byte packet per addition) |
| **Network Partition Impact**| Node operates isolated without issues | Operations fail if Redis unreachable | Local reads/writes proceed without disruption |
| **Best Used For** | Microservices needing reboot persistence | Multi-tenant or memory-constrained pods | High-throughput low-latency distributed APIs |

---

## 🔬 Protocol & Execution Flows

### Model 1: Snapshot Storage Execution Flow

```
[Application Startup]
  │
  ├─► WarmUpBackgroundService
  │     └─► RedisBloomFilterStorage.LoadStreamAsync("users")
  │           └─► GET "bloom:snapshot:users"
  │                 └─► [GZip Decompress] ──► Deserializer ──► Local SIMD BitArray
  │
[Application Runtime: Periodic / Shutdown]
  │
  └─► AutoSaveBackgroundService
        └─► Local SIMD BitArray ──► Serializer ──► [GZip Compress]
              └─► RedisBloomFilterStorage.SaveAsync("users", stream)
                    └─► SET "bloom:snapshot:users" [EX ttl]
```

### Model 2: Live Distributed Filter Pipelined Execution Flow

```
[Application: ContainsAsync("alice@example.com")]
  │
  ├─► 1. Client-Side Hash:
  │      ComputeBaseHashes("alice@example.com") ──► { h1, h2 }
  │
  ├─► 2. Range Reduction (Lemire):
  │      bitPositions = [ GetBitPosition(h1, h2, 0..k-1, sizeInBits) ]
  │
  ├─► 3. Single TCP Round-Trip Pipeline:
  │      IDatabase.CreateBatch()
  │        ├─► batch.StringGetBitAsync("bloom:live:users", pos[0])
  │        ├─► batch.StringGetBitAsync("bloom:live:users", pos[1])
  │        │   ...
  │        └─► batch.StringGetBitAsync("bloom:live:users", pos[k-1])
  │      batch.Execute() ──► Single TCP frame sent to Redis
  │
  └─► 4. Aggregation:
         Task.WhenAll(...) ──► Returns true ONLY if all k bits == 1
```

### Model 3: Hybrid Replicated Filter Pub/Sub Wire Protocol

```
[Node A: Add("user-123")]
  │
  ├─► 1. Compute Base Hashes:
  │      BloomHasher.ComputeBaseHashes("user-123") ──► { h1, h2 }
  │
  ├─► 2. Instant Local Mutation (AVX2 SIMD):
  │      innerFilter.AddWithHashes(h1, h2) ──► 10-50ns (Bit set locally)
  │
  ├─► 3. Wire Serialization (32 Bytes):
  │      ┌────────────────────────┬────────────────────────┬────────────────────────┐
  │      │  OriginNodeId (16B)    │     Hash 1 (8 Bytes)   │     Hash 2 (8 Bytes)   │
  │      │       Guid (UUID)      │      ulong (64-bit)    │      ulong (64-bit)    │
  │      └────────────────────────┴────────────────────────┴────────────────────────┘
  │
  └─► 4. Redis Pub/Sub Broadcast (Fire-and-Forget):
         ISubscriber.PublishAsync("bloom:sync:users", payload, FireAndForget)
           │
           ├──────────────────────────────┐
           ▼                              ▼
     ┌───────────┐                  ┌───────────┐
     │  Node B   │                  │  Node C   │
     │ Unpack:   │                  │ Unpack:   │
     │ Origin != │                  │ Origin != │
     │ MyNodeId  │                  │ MyNodeId  │
     │     │     │                  │     │     │
     │ Apply:    │                  │ Apply:    │
     │ AddWith-  │                  │ AddWith-  │
     │ Hashes()  │                  │ Hashes()  │
     │ (10ns)    │                  │ (10ns)    │
     └───────────┘                  └───────────┘
```

---

## 🧮 Mathematical Engine & Formulas

### 1. Optimal Bit Size ($m$) and Hash Count ($k$)
Given expected capacity $n$ and target false positive probability $p$:

$$m = -\frac{n \ln p}{(\ln 2)^2} \approx -1.442695 \cdot n \log_2 p$$

$$k = \frac{m}{n} \ln 2 \approx -\log_2 p$$

### 2. Kirsch-Mitzenmacher Double Hashing
Bit indices are derived from only two 64-bit base hashes ($h_1, h_2$ generated via MurmurHash3 / XXHash):

$$g_i(x) = h_1(x) + i \cdot h_2(x) \pmod{2^{64}}$$

### 3. Lemire's Fast Range Reduction
Eliminates expensive 64-bit integer modulo (`%`) operations:

$$\text{bitOffset} = \left\lfloor \frac{g_i(x) \times m}{2^{64}} \right\rfloor$$

### 4. Golden Ratio 1-Hash Degeneracy Guard
If $h_2(x) = 0$, all $k$ hash values would collapse to $h_1(x)$. The engine detects this and substitutes Knuth's 64-bit Golden Ratio constant:

$$\phi_{\text{const}} = 0\text{x}9\text{E}3779\text{B}97\text{F}4\text{A}7\text{C}15$$

---

## 📊 Redis Sizing & Capacity Reference

Redis stores strings up to **512 MB** ($2^{32}-1$ bits = 4,294,967,295 bits = 536 MB). A single Redis key can comfortably back hundreds of millions of elements:

| Capacity ($n$) | Target FPR ($p$) | Bit Size ($m$) | Redis Memory | Optimal Hashes ($k$) | Recommended Model |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **10,000** | 1% (0.01) | 95,851 bits | **12 KB** | 7 | Model 1, 2, or 3 |
| **100,000** | 1% (0.01) | 958,506 bits | **117 KB** | 7 | Model 1, 2, or 3 |
| **1,000,000** | 1% (0.01) | 9,585,059 bits | **1.14 MB** | 7 | Model 1, 2, or 3 |
| **10,000,000** | 1% (0.01) | 95,850,584 bits | **11.43 MB** | 7 | Model 2 or 3 |
| **10,000,000** | 0.1% (0.001) | 143,775,876 bits | **17.14 MB** | 10 | Model 2 or 3 |
| **100,000,000**| 1% (0.01) | 958,505,838 bits | **114.26 MB** | 7 | Model 2 (Zero Node RAM) |
| **400,000,000**| 1% (0.01) | 3,834,023,352 bits | **457.05 MB** | 7 | Model 2 (Max single key) |

---

## 📦 Installation

```bash
dotnet add package Wiaoj.BloomFilter.Redis
```

---

## ⚙️ Dependency Injection & Usage Recipes

### Recipe 1: Model 1 — Persistent Redis Snapshot Storage

```csharp
using StackExchange.Redis;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBloomFilter(bf => {
    // 1. Configure Redis snapshot storage
    bf.UseRedisStorage("localhost:6379,abortConnect=false", options => {
        options.KeyPrefix = "bloom:snapshots:";
        options.EnableCompression = true; // Transparent GZip stream compression
        options.Ttl = TimeSpan.FromDays(30);
        options.IgnoreErrors = true;       // Resilient fallback during Redis downtime
    });

    // 2. Register automated persistence lifecycle
    bf.AddAutoSave(options => options.Interval = TimeSpan.FromMinutes(5));
    bf.AddWarmUp(); // Restores snapshot on app boot

    // 3. Register local in-memory filter backed by Redis snapshot storage
    bf.AddFilter<UserTag>("users", expectedItems: 1_000_000, errorRate: 0.01);
});
```

---

### Recipe 2: Model 2 — Live Distributed Remote Filter

Ideal for stateless containers, serverless functions, or multi-tenant architectures:

```csharp
builder.Services.AddBloomFilter(bf => {
    // Shared Redis connection
    bf.UseRedis("localhost:6379");

    // Live remote filter in Redis
    bf.AddDistributedFilter<TransactionTag>(
        "transactions-live",
        expectedItems: 10_000_000,
        errorRate: 0.001,
        options => {
            options.KeyPrefix = "bloom:live:";
            options.Database = 0;
        });
});
```

**Consuming the Filter:**

```csharp
public class TransactionService {
    private readonly IAsyncBloomFilter<TransactionTag> _filter;

    public TransactionService(IAsyncBloomFilter<TransactionTag> filter) {
        this._filter = filter;
    }

    public async Task<bool> IsDuplicateAsync(string transactionId, CancellationToken ct) {
        // Single RTT pipelined GETBIT query
        if (await this._filter.ContainsAsync(transactionId, ct)) {
            return true;
        }

        // Single RTT pipelined SETBIT mutation
        await this._filter.AddAsync(transactionId, ct);
        return false;
    }
}
```

---

### Recipe 3: Model 3 — Hybrid Replicated Filter (L1 SIMD + Pub/Sub Sync)

Ideal for ultra-high-throughput APIs (e.g. rate-limiters, WAF, bot protection):

```csharp
builder.Services.AddBloomFilter(bf => {
    bf.UseRedis("localhost:6379");

    // Hybrid filter
    bf.AddSynchronizedFilter<BlacklistTag>(
        "ip-blacklist",
        expectedItems: 500_000,
        errorRate: 0.01,
        options => {
            options.SyncChannelPrefix = "bloom:sync:";
            options.EnableSnapshotPersistence = true; // Combines with IBloomFilterStorage
        });
});
```

**Consuming the Filter (Lock-free L1 reads):**

```csharp
public class FirewallMiddleware {
    private readonly IBloomFilter<BlacklistTag> _filter;

    public FirewallMiddleware(IBloomFilter<BlacklistTag> filter) {
        this._filter = filter;
    }

    public bool IsBlocked(ReadOnlySpan<byte> clientIp) {
        // 10-50 ns AVX2 SIMD read without any network call
        return this._filter.Contains(clientIp);
    }

    public void BlockIp(ReadOnlySpan<byte> clientIp) {
        // Mutates local bit array immediately + broadcasts 32-byte delta to all peer nodes
        this._filter.Add(clientIp);
    }
}
```

---

## 🛡️ Resilience, Fault-Tolerance & Edge Cases

### Redis Partition / Network Downtime
- **Model 1 (Snapshot Storage):** When `IgnoreErrors = true`, failed snapshot writes or loads log a warning and return `false`/`null`. Local in-memory filters continue to serve reads and writes with zero downtime.
- **Model 2 (Live Distributed):** Commands throw standard `RedisConnectionException` / `RedisTimeoutException`. Utilize Polly or built-in retry policies for transient network partitions.
- **Model 3 (Hybrid Replicated):** If Redis Pub/Sub is unreachable during an `Add()` operation, the exception is caught, logged, and **does not prevent local memory addition**. The local node continues operating. Upon reconnection or scheduled snapshot reload (`ReloadAsync`), consistency is restored.

### Redis Cluster & Sentinel Compatibility
- **Cluster Single-Key Safety:** In Model 1 and Model 2, all operations for a given filter name operate on a single Redis key (`bloom:snapshot:<name>` or `bloom:live:<name>`). Pipelined batch operations never cross hash slots, completely avoiding Redis Cluster `CROSSSLOT` errors.
- **Pub/Sub Cluster Routing:** In Model 3, Redis Cluster natively broadcasts Pub/Sub messages to all nodes in the cluster.

---

## 📈 Performance & Overhead Analysis

| Operation | Model 1 (Snapshot) | Model 2 (Distributed) | Model 3 (Hybrid Replicated) |
| :--- | :--- | :--- | :--- |
| **`Contains(x)` Throughput** | > 25,000,000 ops/sec | ~50,000–100,000 ops/sec | > 25,000,000 ops/sec |
| **`Contains(x)` Latency** | **10–50 ns** | **0.5–2.0 ms** | **10–50 ns** |
| **`Add(x)` Latency** | **10–50 ns** | **0.5–2.0 ms** | **10–50 ns** (Local) + ~0.5ms background async broadcast |
| **Network Bandwidth** | ~1 MB every 5 min (snapshot) | ~100 B per operation | **32 bytes** per unique addition |
| **Garbage Collection** | **0 allocations** (reused pools) | Minimal (pipelined tasks) | **0 allocations** on read path |

---

## ⚙️ Configuration Reference

### `RedisBloomFilterStorageOptions`

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `KeyPrefix` | `string` | `"bloom:snapshot:"` | Redis key prefix for snapshot data. |
| `Ttl` | `TimeSpan?` | `null` | Optional expiration TTL for snapshot keys. |
| `EnableCompression` | `bool` | `false` | When `true`, compresses stream snapshots using GZip. |
| `IgnoreErrors` | `bool` | `false` | When `true`, logs warnings instead of throwing on Redis storage failures. |
| `Database` | `int?` | `null` | Target Redis logical database index (-1 for default). |

### `DistributedBloomFilterOptions`

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `KeyPrefix` | `string` | `"bloom:live:"` | Redis key prefix for distributed bit arrays. |
| `Database` | `int?` | `null` | Target Redis logical database index. |

### `SynchronizedBloomFilterOptions`

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `SyncChannelPrefix` | `string` | `"bloom:sync:"` | Redis Pub/Sub channel prefix for delta replication. |
| `NodeId` | `Guid?` | `null` | Optional explicit node ID (generates `Guid.NewGuid()` if omitted). |
| `EnableSnapshotPersistence`| `bool` | `true` | Whether to persist snapshots to `IBloomFilterStorage`. |

---

## 🧪 Testing

The library includes a hermetic, zero-dependency unit test suite testing all models and edge cases without requiring an active Redis server:

```bash
dotnet run --project tests/Wiaoj.BloomFilter.Redis.Tests.Unit/Wiaoj.BloomFilter.Redis.Tests.Unit.csproj
```
