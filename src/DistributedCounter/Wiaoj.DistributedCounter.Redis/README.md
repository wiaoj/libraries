# Wiaoj.DistributedCounter.Redis

Redis storage provider implementation for the `Wiaoj.DistributedCounter` library, backed by `StackExchange.Redis` and server-side Lua scripts.

This package implements the `ICounterStorage` contract for distributed Redis environments, providing atomic limit checks, sliding window TTL handling, and pipelined batch operations across standalone, cluster, and sentinel deployments.

---

## Installation

```bash
dotnet add package Wiaoj.DistributedCounter.Redis
```

---

## What This Package Contains

- **`RedisCounterStorage`:** Concrete implementation of `ICounterStorage` delegating atomic operations to Redis via `IDatabase`.
- **Atomic Lua Scripts:** Server-side scripts executed during limit evaluations and increments with expiration to eliminate race conditions without distributed locks:
  - `IncrementWithExpire`: Performs atomic `INCRBY` and sets key expiration (`PEXPIRE`) when a TTL is provided.
  - `IncrementIfLessThan`: Checks `current + amount <= limit` before applying `INCRBY`, returning `{isAllowed, currentValue, pttl}` in a single execution.
  - `DecrementIfGreaterThan`: Checks `current - amount >= minLimit` before applying `DECRBY`, returning remaining threshold capacity.
- **Fluent DI Builder Extensions:** Extension methods on `IDistributedCounterBuilder` and `CounterConfiguration` for configuring Redis backends.

---

## Dependency Injection Setup

### 1. Global Default Configuration

Configure Redis as the global storage backend for all distributed counters:

#### Using a Connection String
```csharp
using Wiaoj.DistributedCounter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedCounter(counter => {
    counter.UseRedis("localhost:6379,abortConnect=false");
    counter.AddAutoFlush();

    counter.AddImmediateCounter<RateLimitTag>();
    counter.AddBufferedCounter<PageViewsTag>();
});
```

#### Using `ConfigurationOptions`
```csharp
using StackExchange.Redis;
using Wiaoj.DistributedCounter;

builder.Services.AddDistributedCounter(counter => {
    ConfigurationOptions redisOptions = new() {
        EndPoints = { "redis-node1:6379", "redis-node2:6379" },
        Password = "SecretPassword",
        ConnectTimeout = 5000,
        SyncTimeout = 3000
    };

    counter.UseRedis(redisOptions);
    counter.AddAutoFlush();
});
```

#### Using an Existing DI-Registered `IConnectionMultiplexer`
```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddDistributedCounter(counter => {
    counter.UseRedis();
    counter.AddAutoFlush();
});
```

---

### 2. Multi-Cluster and Keyed Service Routing

Route specific counter categories to independent Redis instances or clusters using .NET Keyed Services:

```csharp
using StackExchange.Redis;
using Wiaoj.DistributedCounter;

// Register multiple keyed multiplexers in DI
builder.Services.AddKeyedSingleton<IConnectionMultiplexer>(
    "security-cluster",
    (_, _) => ConnectionMultiplexer.Connect("security-redis:6379"));

builder.Services.AddKeyedSingleton<IConnectionMultiplexer>(
    "analytics-cluster",
    (_, _) => ConnectionMultiplexer.Connect("analytics-redis:6379"));

builder.Services.AddDistributedCounter(counter => {
    // Default fallback storage
    counter.UseRedisKeyed("analytics-cluster");

    // Route security quota counter specifically to security-cluster
    counter.AddImmediateCounter<AuthRateLimitTag>(cfg => {
        cfg.UseRedisKeyed("security-cluster");
    });

    // Route telemetry to analytics-cluster
    counter.AddBufferedCounter<TelemetryTag>(cfg => {
        cfg.UseRedisKeyed("analytics-cluster");
    });
});
```

---

## How Atomic Limit Checks Work

When `TryIncrementAsync` or `TryDecrementAsync` is called:

1. `RedisCounterStorage` evaluates the request via a parameterized Lua script (`ScriptEvaluateAsync`).
2. The script checks whether the target key currently exists and parses its numeric value (treating non-existent or expired keys as 0).
3. If the resulting value is within the limit:
   - The key is updated using `INCRBY` / `DECRBY`.
   - If the key is new and carries an expiration, `PEXPIRE` is applied.
   - The script returns `1` (allowed), the updated numeric value, and the current remaining `PTTL`.
4. If the resulting value exceeds the limit:
   - No mutations are applied to the key.
   - The script returns `0` (denied), the unchanged current value, and the remaining `PTTL`.

This guarantees that limits are enforced strictly without requiring distributed locks or multiple network round-trips.

---

## Batch Operations and Pipelining

### Pipelined Batch Increments (`BatchIncrementAsync`)

During background flush cycles triggered by `CounterAutoFlushService`, pending counter updates are compiled into an `IBatch` pipeline:

```csharp
// Dispatches multiple ScriptEvaluateAsync calls in a single network round-trip
ReadOnlyMemory<CounterUpdate> updates = new CounterUpdate[] {
    new(new CounterKey("metric:orders"), 10, CounterExpiry.Infinite),
    new(new CounterKey("metric:logins"), 2, CounterExpiry.FromMinutes(5))
};

Memory<long> results = new long[2];
await redisStorage.BatchIncrementAsync(updates, results, cancellationToken);
```

### Multi-Key Reads (`GetManyAsync`)

Batch key queries convert `CounterKey` spans to `RedisKey` arrays using `ArrayPool<RedisKey>` and execute an `MGET` (`StringGetAsync`) call to fetch values concurrently.

---

## License

This project is licensed under the MIT License.