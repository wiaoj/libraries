# Wiaoj.BloomFilter

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Wiaoj.BloomFilter** is a high-performance, thread-safe, and persistent implementation of the probabilistic Bloom Filter data structure for .NET 8+.

It is designed for high-throughput scenarios where memory efficiency and zero-allocation operations are critical. Unlike standard in-memory implementations, this library manages the full lifecycle of the filter, including **asynchronous persistence**, **automatic sharding**, **failure recovery**, and **background hydration**.

## 🚀 Key Features

*   **Zero-Allocation Architecture:** Uses `ArrayPool` and `PooledBitArray` to minimize Garbage Collector (GC) pressure. Seeding and lookup operations use `ReadOnlySpan<byte>` for maximum throughput.
*   **SIMD Optimized:** Hashing algorithms utilize SIMD (Single Instruction, Multiple Data) instructions where supported by the CPU.
*   **Robust Persistence:**
    *   **Atomic Writes:** Prevents data corruption during power failures using temporary files and atomic moves.
    *   **Snapshotting:** Takes non-blocking memory snapshots to allow reads/writes to continue while saving to disk.
    *   **Compression:** Built-in GZip support for optimizing storage space.
*   **Advanced Lifecycle Management:**
    *   **Auto-Save:** Background service periodically persists "dirty" filters.
    *   **Warm-Up:** Preloads filters into memory on application startup.
    *   **Auto-Reseed:** Automatically triggers data re-population from external sources (DB, API) if corruption is detected.
*   **Smart Sharding:** Automatically splits massive filters into smaller shards based on a configurable threshold (e.g., 100MB) to overcome CLR object size limits.
*   **Type-Safe Dependency Injection:** Supports typed interfaces (e.g., `IBloomFilter<UserBlacklistTag>`) to avoid "magic string" errors.
*   **Highly Configurable:** Fully integrated with `Microsoft.Extensions.Configuration` (appsettings.json) and Options Pattern.

---

## 📦 Installation

```bash
dotnet add package Wiaoj.BloomFilter
```

---

## ⚡ Quick Start

### 1. Configuration (appsettings.json)

The library supports a structured configuration to tune performance, storage, and lifecycle.

```json
{
  "BloomFilter": {
    "Storage": {
      "Path": "BloomData",
      "EnableCompression": true,
      "IgnoreErrors": false
    },
    "Performance": {
      "EnableSimd": true,
      "GlobalHashSeed": 123456789
    },
    "Lifecycle": {
      "AutoSaveInterval": "00:05:00",
      "EnableWarmUp": true,
      "EnableIntegrityCheck": true,
      "AutoReseed": true,
      "ShardingThresholdBytes": 104857600
    },
    "Filters": {
      "url-blacklist": {
        "ExpectedItems": 1000000,
        "ErrorRate": 0.001
      },
      "ip-whitelist": {
        "ExpectedItems": 50000,
        "ErrorRate": 0.01
      }
    }
  }
}
```

### 2. Registration (Program.cs)

Register the services with a single line. The library automatically binds the configuration.

```csharp
using Wiaoj.BloomFilter;

var builder = WebApplication.CreateBuilder(args);

// Add Bloom Filter services
builder.Services.AddBloomFilter(setup => {
    // Map a Typed Tag to a named filter configuration
    setup.MapFilter<UrlBlacklistTag>("url-blacklist");
});

var app = builder.Build();
```

### 3. Usage (Minimal API)

Inject `IBloomFilter<T>` directly into your endpoints.

```csharp
app.MapGet("/check-url", (
    [FromQuery] string url, 
    [FromServices] IBloomFilter<UrlBlacklistTag> filter) => 
{
    // Fast, zero-allocation check
    bool mightContain = filter.Contains(url);

    return mightContain 
        ? Results.Ok("URL might be blacklisted (Probabilistic match).") 
        : Results.Ok("URL is safe (Definite negative).");
});
```

---

## 🏗 Architecture & Concepts

### 1. Typed Filters (`IBloomFilter<T>`)
To prevent using the wrong filter for a specific domain entity, the library encourages the use of Marker Types (Tags).

```csharp
// Define a marker record
public record UrlBlacklistTag;

// Inject it safely
public class SecurityService(IBloomFilter<UrlBlacklistTag> filter) { ... }
```

### 2. Seeding & Auto-Reseed Strategy
Populating a Bloom Filter from a database can be resource-intensive. `IAutoBloomFilterSeeder` allows you to define how a filter should be filled. This is used for:
1.  **Initial Population:** Running a background job to fill the cache.
2.  **Disaster Recovery:** If the library detects file corruption (Checksum mismatch), it automatically deletes the corrupt file and triggers the Seeder to refill the filter without stopping the application.

**Example Seeder:**

```csharp
public class BlacklistSeeder : IAutoBloomFilterSeeder
{
    // Matches the config name
    public FilterName FilterName => "url-blacklist"; 

    public async Task SeedAsync(IPersistentBloomFilter filter, CancellationToken ct)
    {
        // Stream data from DB/API and add to filter
        // Uses ReadOnlySpan<byte> for zero allocation
        await foreach(var url in _repository.GetMaliciousUrlsAsync(ct)) 
        {
            filter.Add(Encoding.UTF8.GetBytes(url));
        }
        
        // Persist immediately after seeding
        await filter.SaveAsync(ct);
    }
}
```

### 3. Concurrency Model
The library uses `ReaderWriterLockSlim` to manage thread safety:
*   **Add / Contains:** Acquires a `ReadLock`. This allows thousands of concurrent operations. It only blocks during a Reload/Swap operation.
*   **Save:** Takes a snapshot of the memory under a `ReadLock` (very fast), then writes to disk asynchronously without blocking new additions.
*   **Reload:** Acquires a `WriteLock` only for the brief moment of swapping the internal bit array pointer.

### 4. Persistence Layout
Data is stored in binary format with a header containing metadata and checksums.

```text
[Header: 36 Bytes]
+----------------+---------+----------+-------------+------------+-------------+
| Magic ("WBF1") | Version | Checksum | SizeInBits  | HashCount  | Fingerprint |
+----------------+---------+----------+-------------+------------+-------------+
|     4 Bytes    | 4 Bytes |  8 Bytes |   8 Bytes   |   4 Bytes  |   8 Bytes   |
+----------------+---------+----------+-------------+------------+-------------+

[Body: Variable]
+------------------------------------------------------------------------------+
| Bit Array (Raw Bytes) ...                                                    |
+------------------------------------------------------------------------------+
```

*   **Checksum:** XXHash64 of the bit array for integrity validation.
*   **Fingerprint:** Hash of the configuration (Capacity + ErrorRate + Seed). Prevents loading a file with mismatched settings.

---

## ⚙️ Advanced Configuration

### Storage Providers
By default, the library uses the `FileSystem` provider. It saves files to the path specified in `Storage:Path`. The library is architected to support other providers (e.g., Redis) in the future via `IBloomFilterStorage` interface.

### Performance Tuning
*   **`EnableSimd`**: Uses `System.Runtime.Intrinsics` to parallelize bitwise operations and hashing on supported hardware (AVX2/NEON).
*   **`ShardingThresholdBytes`**: If a calculated filter size exceeds this limit (default 100MB), the provider automatically splits it into multiple smaller filters (`_s0`, `_s1`, ...). This avoids LOH (Large Object Heap) fragmentation and improves GC performance.

---

## 🛡 Handling Data Integrity

1.  **Corruption Detection:** On load, the header checksum is calculated against the body.
2.  **Configuration Mismatch:** If you change `ErrorRate` or `ExpectedItems` in `appsettings.json`, the stored file's fingerprint won't match.
3.  **Automatic Recovery:** In both cases above, if `AutoReseed` is enabled, the library will:
    *   Log a warning.
    *   Delete the invalid file.
    *   Initialize a fresh in-memory filter.
    *   Trigger the registered `IAutoBloomFilterSeeder` in a background thread.

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:
1.  Fork the repository.
2.  Create a feature branch (`git checkout -b feature/amazing-feature`).
3.  Commit your changes.
4.  Open a Pull Request.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).