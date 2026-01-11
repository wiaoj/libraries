# 🌸 Wiaoj.BloomFilter

**Enterprise-Grade, High-Performance, Persistent Bloom Filter Library for .NET 10**

`Wiaoj.BloomFilter` is a robust, thread-safe, and allocation-friendly probabilistic data structure library designed for high-throughput distributed systems. It goes beyond standard implementations by offering built-in persistence, memory pooling, structured logging, and strict data integrity checks.

## 🚀 Key Features

*   **🔒 Concurrency Safe:** Uses `ReaderWriterLockSlim` and `Interlocked` operations to allow massive concurrent reads/writes without data loss or race conditions during reloads.
*   **💾 Persistent & Resilient:**
    *   **Stream-Based I/O:** Hydrates filters from disk/network streams without loading the entire file into LOH (Large Object Heap).
    *   **Data Integrity:** Implements a binary header protocol with **XXHash64 checksums** to prevent loading corrupted data.
    *   **Auto-Save:** Background service automatically persists dirty filters.
*   **⚡ High Performance:**
    *   **Zero-Allocation Hot Paths:** Uses `stackalloc`, `ReadOnlySpan<T>`, and `ArrayPool<T>` to minimize GC pressure.
    *   **Fast Hashing:** Powered by .NET's `System.IO.Hashing.XxHash64`.
*   **🧠 Smart Lifecycle:**
    *   **Async Lazy Loading:** Filters are loaded from storage only upon first access (Cache Stampede protection).
    *   **Typed Injection:** Inject `IBloomFilter<UserTag>` directly into your services.

## 📦 Installation

```bash
dotnet add package Wiaoj.BloomFilter
```

## ⚡ Quick Start

### 1. Define Filter Tags (Marker Classes)
Create empty classes to act as strong types for your filters. This prevents "magic string" errors.

```csharp
public class UserTag { }
public class BlacklistTag { }
```

### 2. Configure Dependency Injection
Register the library in your `Program.cs`. The Fluent Builder API makes configuration seamless.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Wiaoj Bloom Filter
builder.Services.AddWiaojBloomFilter(setup => {
    
    // 1. Configure Global Options
    setup.Configure(options => {
        options.AutoSaveInterval = TimeSpan.FromMinutes(10);
        options.EnableIntegrityCheck = true;
        options.EnableVerboseLogging = false; // Keep false for production performance
    });

    // 2. Register Storage Provider (e.g., Redis or FileSystem)
    setup.AddStorage<MyFileSystemStorage>();

    // 3. Register Filters (Configuration + DI Injection)
    // "users": 10 Million items, 1% error rate
    setup.AddFilter<UserTag>("users-v1", 10_000_000, 0.01);
    
    // "ips": 100k items, 0.1% error rate
    setup.AddFilter<BlacklistTag>("blocked-ips", 100_000, 0.001);

    // 4. Enable Background Auto-Save
    setup.AddAutoSave();
});
```

### 3. Implement Storage Provider
Implement `IBloomFilterStorage` to tell the library where to save/load bits (Disk, Redis, S3, etc.).

```csharp
public class MyFileSystemStorage : IBloomFilterStorage {
    public async ValueTask SaveAsync(string name, BloomFilterConfiguration config, Stream source, CancellationToken ct) {
        using var fs = new FileStream($"{name}.bin", FileMode.Create);
        await source.CopyToAsync(fs, ct);
    }

    public async ValueTask<(BloomFilterConfiguration, Stream)?> LoadStreamAsync(string name, CancellationToken ct) {
        if (!File.Exists($"{name}.bin")) return null;
        
        var fs = new FileStream($"{name}.bin", FileMode.Open, FileAccess.Read);
        // Configuration is handled by the provider, returning null config is acceptable here
        return (null!, fs);
    }
}
```

### 4. Inject and Use
Inject the typed filter interface directly into your services. No factories, no lookups.

```csharp
public class AccountService(IBloomFilter<UserTag> userFilter, ILogger<AccountService> logger) {
    
    public async Task RegisterUserAsync(string username) {
        // 1. Check Bloom Filter (Fast, In-Memory)
        // If it returns FALSE, the user DEFINITELY does not exist. No DB call needed.
        if (userFilter.Contains(username)) {
            logger.LogWarning("Potential duplicate username '{User}'. Checking Database...", username);
            // ... Perform actual DB check ...
        }

        // ... Save user to DB ...

        // 2. Add to Bloom Filter
        userFilter.Add(username);
    }
}
```

## 🛠 Advanced Scenarios

### Seeding (Hydration)
Populating a filter from an existing database (100M+ records) efficiently without crashing memory.

```csharp
public class SeedingWorker : BackgroundService {
    private readonly IBloomFilterSeeder _seeder;
    // ... dependencies ...

    protected override async Task ExecuteAsync(CancellationToken ct) {
        // Stream data from DB using IAsyncEnumerable (EF Core)
        var userStream = dbContext.Users
            .AsNoTracking()
            .Select(u => u.Username)
            .AsAsyncEnumerable();

        // Seeds the filter efficiently chunk-by-chunk
        await _seeder.SeedAsync("users-v1", userStream, ct);
    }
}
```

### Manual Reloading
If you are running in a distributed environment (e.g., Kubernetes) and another instance updated the filter in Redis, you can trigger a reload.

```csharp
public class AdminController(IBloomFilterProvider provider) : ControllerBase {
    [HttpPost("reload-filters")]
    public async Task<IActionResult> Reload() {
        var filter = await provider.GetAsync("users-v1");
        
        // This stops the world (WriteLock), swaps the bits from storage, and resumes.
        await filter.ReloadAsync(); 
        
        return Ok();
    }
}
```

## 📐 Architecture & Performance

### Memory Layout
*   **PooledBitArray:** Instead of standard `BitArray` (which uses `int[]` on Managed Heap), we use `ArrayPool<ulong>.Shared`. This prevents **Large Object Heap (LOH)** fragmentation for massive filters.
*   **Snapshotting:** When `SaveAsync` is called, we acquire a fast `ReadLock`, copy bits to a pooled buffer, release the lock, and then stream to disk. This ensures the filter remains responsive during I/O operations.

### Binary Protocol
Files are saved with a strict binary header to ensure integrity:
```
[MagicBytes (4)] [Version (4)] [XXHash64 Checksum (8)] [Payload...]
```
If a file is corrupted or incomplete, `DataIntegrityException` is thrown, preventing the application from working with bad data.

## 🤝 Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## 📄 License
[MIT](LICENSE)