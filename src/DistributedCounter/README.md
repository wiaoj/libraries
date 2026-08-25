# Wiaoj.DistributedCounter

A modular distributed counter library for .NET supporting strongly-typed marker tags, dual synchronization strategies (`Immediate` and `Buffered`), multi-storage routing, and atomic limit enforcement.

---

## Packages

| Package | Description |
| :--- | :--- |
| **`Wiaoj.DistributedCounter.Abstractions`** | Core contracts, interfaces (`IDistributedCounter`, `ICounterStorage`), and lightweight value primitives (`CounterKey`, `CounterValue`, `CounterExpiry`) with zero third-party runtime dependencies. |
| **`Wiaoj.DistributedCounter`** | Core runtime engine, in-memory storage implementation, background periodic `CounterAutoFlushService`, object pooling, and DI builder extensions. |
| **`Wiaoj.DistributedCounter.Redis`** | Redis storage provider backed by `StackExchange.Redis` and atomic Lua scripts. Supports single instances, connection options, and keyed multiplexers. |
| **`Wiaoj.DistributedCounter.Testing`** | Test doubles (`FakeCounterStorage`), assertion extensions, and test context harness for unit and integration testing. |

---

## Architecture Overview

```
                      ┌───────────────────────────────────────────────┐
                      │    Wiaoj.DistributedCounter.Abstractions      │
                      │  (Contracts, Primitives, Options & Structs)   │
                      └───────────────────────┬───────────────────────┘
                                              │
                     ┌────────────────────────┴────────────────────────┐
                     │                                                 │
                     ▼                                                 ▼
     ┌───────────────────────────────┐                 ┌───────────────────────────────┐
     │   Wiaoj.DistributedCounter    │                 │ Wiaoj.DistributedCounter.     │
     │     (Core Engine, Memory,     │                 │            Testing            │
     │      AutoFlush & Factory)     │                 │   (Fakes, Assertions & DTOs)  │
     └───────────────┬───────────────┘                 └───────────────────────────────┘
                     │
                     ▼
     ┌───────────────────────────────┐
     │ Wiaoj.DistributedCounter.     │
     │             Redis             │
     │    (StackExchange.Redis &     │
     │         Lua Scripts)          │
     └───────────────────────────────┘
```

---

## Quick Start

### 1. Installation

Install the core package and the storage backend for your application:

```bash
dotnet add package Wiaoj.DistributedCounter
dotnet add package Wiaoj.DistributedCounter.Redis
```

### 2. Dependency Injection Setup

Register the counter services in your application startup:

```csharp
using Wiaoj.DistributedCounter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedCounter(counter => {
    // Set global default storage
    counter.UseRedis("localhost:6379,abortConnect=false");

    // Enable background periodic auto-flush for buffered counters
    counter.AddAutoFlush();

    // 1. Direct storage writes (Immediate strategy)
    counter.AddImmediateCounter<RateLimitTag>();

    // 2. Local in-memory batching with periodic background flush (Buffered strategy)
    counter.AddBufferedCounter<PageViewsTag>();

    // 3. Dedicated in-memory storage override for pod-local counters
    counter.AddCounter<LocalWorkerTag>(cfg => {
        cfg.Strategy = CounterStrategy.Buffered;
        cfg.UseInMemory();
    });
});
```

### 3. Basic Usage via Dependency Injection

```csharp
public sealed class CheckoutService(
    IDistributedCounter<RateLimitTag> rateLimiter,
    IDistributedCounter<PageViewsTag> viewCounter) {

    public async Task<IResult> ProcessOrderAsync(string clientIp, CancellationToken cancellationToken) {
        // Evaluate limit atomically against Redis (max 5 requests per minute)
        CounterLimitResult limitResult = await rateLimiter
            .ForKey(clientIp)
            .TryIncrementAsync(
                amount: 1, 
                limit: 5, 
                expiry: CounterExpiry.FromMinutes(1), 
                cancellationToken: cancellationToken);

        if (!limitResult.IsAllowed) {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        // Increment buffered telemetry counter in local memory
        await viewCounter.IncrementAsync(cancellationToken);

        return Results.Ok();
    }
}

// Marker tags
public sealed class RateLimitTag;
public sealed class PageViewsTag;
public sealed class LocalWorkerTag;
```

---

## Core Concepts

### Synchronization Strategies

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            IMMEDIATE STRATEGY                               │
│                                                                             │
│  Caller ───► [IDistributedCounter] ───► (Storage Call) ───► [ ICounterStorage ] │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                            BUFFERED STRATEGY                                │
│                                                                             │
│  Caller ───► [ Atomic In-Memory RAM ]                                       │
│                      │                                                      │
│                      ▼ (Periodic Batch Flush)                               │
│         [ CounterAutoFlushService ] ───► (Batch Call) ───► [ ICounterStorage ] │
└─────────────────────────────────────────────────────────────────────────────┘
```

| Strategy | Description | Best Used For |
| :--- | :--- | :--- |
| **`Immediate`** | Synchronously dispatches each operation to the configured `ICounterStorage`. | API rate limiting, financial quotas, circuit breakers, and concurrency locks. |
| **`Buffered`** | Aggregates deltas in local memory using `Interlocked` operations and flushes them periodically in batches. | High-frequency telemetry, page views, search analytics, and diagnostics. |

---

### Atomic Limit Enforcement

`TryIncrementAsync` and `TryDecrementAsync` evaluate and apply limit boundaries atomically without distributed locks:

```csharp
CounterLimitResult result = await stockCounter.TryIncrementAsync(
    amount: requestedItems,
    limit: maxStock,
    expiry: CounterExpiry.Infinite,
    cancellationToken: cancellationToken);

if (result.IsAllowed) {
    long remainingStock = result.Remaining;
} else {
    // Rejected: Value was not modified
    TimeSpan? windowResetTtl = result.Ttl;
}
```

---

### Expiration and Sliding Windows (`CounterExpiry`)

`CounterExpiry` manages sliding time-to-live policies:

```csharp
// Explicit durations
CounterExpiry expiry = CounterExpiry.FromSeconds(30);
CounterExpiry window = CounterExpiry.FromMinutes(15);
CounterExpiry persistent = CounterExpiry.Infinite;

// Implicit conversion from TimeSpan
await counter.IncrementAsync(amount: 1, expiry: TimeSpan.FromSeconds(60), cancellationToken);
```

---

### Multi-Storage Routing

Route specific counter categories to different storage backends or keyed service instances:

```csharp
builder.Services.AddDistributedCounter(counter => {
    // Default storage
    counter.UseRedis("redis-main:6379");

    // Route security counters to an isolated Redis instance
    counter.AddImmediateCounter<PaymentQuotaTag>(cfg => {
        cfg.UseRedis("redis-secure:6379");
    });

    // Route telemetry counters exclusively to in-memory storage
    counter.AddBufferedCounter<DiagnosticTag>(cfg => {
        cfg.UseInMemory();
    });

    // Route to a custom ICounterStorage implementation
    counter.AddCounter<DatabaseTag>(cfg => {
        cfg.UseStorage<CustomDatabaseCounterStorage>();
    });
});
```

---

### Batch Operations (`IDistributedCounterService`)

Retrieve multiple counter values in a single batched query using pooled data buffers:

```csharp
public sealed class DashboardService(IDistributedCounterService counterService) {
    public async Task RenderDashboardAsync(CancellationToken cancellationToken) {
        string[] metrics = ["orders_daily", "active_users", "failed_jobs"];

        using CounterValueCollection values = await counterService.GetValuesAsync(
            metrics, 
            cancellationToken);

        long orders = values["orders_daily"].Value;
        long users = values["active_users"].Value;
    }
}
```

---

## Observability

The library exports OpenTelemetry-compatible diagnostics under the name `Wiaoj.DistributedCounter`:

- **Metrics (`Meter`):**
  - `distributed_counter.increments`: Total increments requested (tags: `name`, `strategy`).
  - `distributed_counter.flushes`: Total background batch flushes executed.
  - `distributed_counter.flush_duration`: Latency histogram of storage batch sync operations in milliseconds.
- **Traces (`ActivitySource`):**
  - `FlushBatch`: Activity tracing the lifecycle of auto-flush batch synchronization.
  - `SelfHealingDrift`: Activity event recorded when remote value divergence is reconciled.

---

## Testing

Use the `Wiaoj.DistributedCounter.Testing` package to write isolated unit and integration tests without Redis:

```csharp
[Fact]
public async Task TestRateLimiter() {
    FakeTimeProvider timeProvider = new();
    DistributedCounterTestContext context = new(timeProvider, options => {
        options.AddImmediateCounter<RateLimitTag>();
    });

    IDistributedCounterFactory factory = context.CreateFactory();
    IDistributedCounter<RateLimitTag> counter = factory.Create<RateLimitTag>();

    await counter.IncrementAsync("user_1", 1, CounterExpiry.FromMinutes(1), CancellationToken.None);

    context.Storage.ShouldHaveValue(counter.ForKey("user_1").Key, 1);
}
```

---

## License

This project is licensed under the MIT License.