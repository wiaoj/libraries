# Wiaoj.DistributedCounter

The core runtime engine, in-memory storage provider, background synchronization service, and dependency injection integration for the `Wiaoj.DistributedCounter` library.

---

## Installation

```bash
dotnet add package Wiaoj.DistributedCounter
```

---

## What This Package Contains

- **`DistributedCounterFactory`:** Resolves and caches counter instances (`IDistributedCounter`) by name or marker tag (`TTag`), applying configured synchronization strategies and dedicated storage backends.
- **`TypedDistributedCounterWrapper<TTag>`:** Generic wrapper registered in DI as `IDistributedCounter<TTag>`, delegating operations to factory-resolved counters and supporting sub-key scoping (`.ForKey(key)`).
- **`InMemoryCounterStorage`:** Thread-safe `ICounterStorage` implementation using `ConcurrentDictionary`, `TimeProvider`-based sliding expiration, and CAS (Compare-And-Swap) loops for quota checks.
- **`CounterAutoFlushService`:** An `IHostedService` background worker that periodically collects pending in-memory deltas from buffered counters and executes batch writes against their respective storage providers.
- **`DistributedCounterService`:** Service for batch counter queries (`GetValuesAsync`), manual flush triggers (`FlushAllAsync`), and system-wide resets (`ResetAllAsync`).
- **Object Pool Integration:** Reuses internal `Dictionary<string, CounterValue>` buffers via `Wiaoj.ObjectPool` to reduce heap allocations on batch retrieval paths.

---

## Dependency Injection Setup

### Basic Registration (In-Memory)

```csharp
using Wiaoj.DistributedCounter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedCounter(counter => {
    // Set in-memory storage as default backend
    counter.UseInMemory();

    // Enable background periodic auto-flush for buffered counters
    counter.AddAutoFlush();

    // Register an immediate counter (direct storage writes)
    counter.AddImmediateCounter<RateLimitTag>();

    // Register a buffered counter (in-memory aggregation with periodic flush)
    counter.AddBufferedCounter<PageViewsTag>();
});
```

---

### Configuration Options

```csharp
builder.Services.AddDistributedCounter(counter => {
    counter.Configure(options => {
        options.GlobalKeyPrefix = "app:counters:";
        options.DefaultStrategy = CounterStrategy.Buffered;
        options.AutoFlushInterval = TimeSpan.FromSeconds(5);
    });

    counter.UseInMemory();
    counter.AddAutoFlush();

    // Per-tag strategy and storage overrides
    counter.AddCounter<SecurityTag>(cfg => {
        cfg.Strategy = CounterStrategy.Immediate;
        cfg.UseInMemory();
    });
});
```

---

## Synchronization Strategies

### 1. `Immediate` (`ImmediateDistributedCounter`)
- Every call to `IncrementAsync`, `DecrementAsync`, `TryIncrementAsync`, `TryDecrementAsync`, `SetAsync`, or `ResetAsync` executes directly against the configured `ICounterStorage`.
- Recommended for rate limiting, quotas, and scenarios requiring absolute cross-instance consistency.

### 2. `Buffered` (`BufferedDistributedCounter`)
- Calls to `IncrementAsync` and `DecrementAsync` update a local delta in memory using atomic operations (`Interlocked`).
- Calls to `GetValueAsync` return the cached base value plus local pending deltas.
- Calls to `TryIncrementAsync` or `TryDecrementAsync` first force a local flush to storage, then evaluate the limit remotely.
- Pending deltas are collected and committed in batches by `CounterAutoFlushService`.
- If an external process changes the remote value, the counter detects the drift during flush response synchronization and adjusts its local base value (self-healing).
- If a storage flush fails, the uncommitted local delta is rolled back into memory to prevent data loss.

---

## In-Memory Storage (`InMemoryCounterStorage`)

`InMemoryCounterStorage` provides a standalone storage backend without external dependencies:

- **Sliding Expiration:** Tracks expiration timestamps per key relative to the injected `TimeProvider`.
- **Atomic Quota Checks:** Implements `TryIncrementAsync` and `TryDecrementAsync` via lock-free update loops on `ConcurrentDictionary`.
- **Batch Operations:** Implements `BatchIncrementAsync` and `GetManyAsync` for multi-key updates and queries.

```csharp
// Standalone usage outside Microsoft DI
TimeProvider timeProvider = TimeProvider.System;
ICounterStorage storage = new InMemoryCounterStorage(timeProvider);

CounterKey key = new("test:key");
await storage.AtomicIncrementAsync(key, 1, CounterExpiry.FromMinutes(1), CancellationToken.None);
```

---

## Background Worker (`CounterAutoFlushService`)

When enabled via `.AddAutoFlush()`, `CounterAutoFlushService`:

1. Runs on a `PeriodicTimer` driven by `options.AutoFlushInterval` and `TimeProvider`.
2. Gathers all active `BufferedDistributedCounter` instances from `IBufferedCounterSource`.
3. Groups counters by their assigned `ICounterStorage` reference.
4. Rents array buffers from `ArrayPool<T>` and executes `BatchIncrementAsync` on each storage group.
5. Synchronizes resulting remote values with local counter base values.
6. Performs a final batch flush when `StopAsync` is invoked during application shutdown.

---

## Observability

### OpenTelemetry Metrics (`Wiaoj.DistributedCounter`)

- `distributed_counter.increments` (`Counter<long>`): Total increment calls (tags: `name`, `strategy`).
- `distributed_counter.flushes` (`Counter<long>`): Total background flush runs.
- `distributed_counter.flush_duration` (`Histogram<double>` in `ms`): Batch flush execution duration.

### OpenTelemetry Traces (`Wiaoj.DistributedCounter`)

- `FlushBatch`: Activity wrapping the batch update extraction, serialization, and storage execution.
- `SelfHealingDrift`: Event attached to the activity when remote drift is detected.

---

## License

This project is licensed under the MIT License.