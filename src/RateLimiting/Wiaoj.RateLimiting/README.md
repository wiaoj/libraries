# Wiaoj.RateLimiting

Core execution runtime, algorithms, policy orchestration, and resilience decorators for the `Wiaoj.RateLimiting` library.

This package contains the concrete rate limiting algorithm engines, policy registration builders, in-memory state stores, distributed coordination via `Wiaoj.DistributedCounter`, and OpenTelemetry diagnostics.

---

## Installation

```bash
dotnet add package Wiaoj.RateLimiting
```

---

## Included Algorithms

### 1. `FixedWindowRateLimiter` (Distributed / Storage-Backed)
- Counts operations against a fixed time window using `IDistributedCounter.TryIncrementAsync`.
- Operates atomically in a single round-trip against the configured storage backend (Redis Lua script or In-Memory CAS).

### 2. `SlidingWindowRateLimiter` (Distributed / Storage-Backed)
- Approximates a sliding lookback window by blending the previous window's count with the current window based on elapsed time weight.
- Speculatively increments the current counter and executes a rollback decrement if the estimated total exceeds the limit.

### 3. `GcraRateLimiter` (Distributed / Storage-Backed)
- Implements the Generic Cell Rate Algorithm (GCRA) by tracking a single Theoretical Arrival Time (TAT) in UTC ticks.
- Executes an optimistic Compare-And-Swap (CAS) retry loop via `IDistributedCounter.TryCompareExchangeAsync` to ensure atomicity across multi-node clusters.

### 4. `TokenBucketRateLimiter` (In-Memory)
- Tracks token capacity and continuous refill rate in local memory using `ConcurrentDictionary` and atomic CAS updates.
- Absorbs sudden bursts up to capacity, then throttles to the steady refill rate.

### 5. `SlidingWindowLogRateLimiter` (In-Memory)
- Maintains an in-memory timestamp log per key, evicting expired entries on each acquisition.
- Guarantees exact rolling lookback evaluation with no window boundary burst anomalies.

### 6. `LeakyBucketQueueRateLimiter` (In-Memory / Traffic Shaping)
- A traffic shaper that holds admitted requests inside `TryAcquireAsync` using `TimeProvider.Delay` until their scheduled turn arrives.
- Rejects requests immediately only when the maximum queue backlog capacity is exceeded.
- Automatically rolls back backlog reservations if the caller's `CancellationToken` is triggered during wait.

### 7. `CompositeRateLimiter`
- Evaluates an ordered sequence of algorithm tiers.
- A request is permitted only if all configured tiers allow it. Returns the minimum remaining capacity among allowing tiers, or the maximum `RetryAfter` if any tier denies.

---

## Dependency Injection Setup

### Configuring Named and Typed Policies

```csharp
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure underlying counter backend
builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

// 2. Configure rate limiting policies
builder.Services.AddWiaojRateLimiting(limiter => {
    // Named policy: Fixed window for auth
    limiter.AddPolicy("auth", policy => {
        policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(1))
              .WithFailOpen();
    });

    // Named policy: Burst-tolerant GCRA for searches
    limiter.AddPolicy("search", policy => {
        policy.UseGcra(limit: 20, period: TimeSpan.FromSeconds(1))
              .WithNegativeCaching();
    });

    // Strongly-typed policy for payments
    limiter.AddPolicy<PaymentPolicy>(policy => {
        policy.UseTokenBucket(capacity: 10, window: TimeSpan.FromMinutes(1));
    });

    // Multi-tier composite policy
    limiter.AddPolicy("tiered_api", policy => {
        policy.UseComposite(
            tier1 => tier1.UseFixedWindow(limit: 10, window: TimeSpan.FromSeconds(1)),
            tier2 => tier2.UseSlidingWindow(limit: 1000, window: TimeSpan.FromHours(1))
        );
    });

    // Default fallback policy
    limiter.UseDefaultPolicy(policy => {
        policy.UseFixedWindow(limit: 100, window: TimeSpan.FromMinutes(1));
    });
});
```

---

## Per-Policy Storage Routing

By default, distributed algorithms (`FixedWindow`, `SlidingWindow`, `Gcra`) route counter operations to the global storage configured in `AddDistributedCounter`.

Specific policies can be routed to dedicated storage instances, keyed Redis multiplexers, or custom database backends directly within the policy builder:

```csharp
builder.Services.AddWiaojRateLimiting(limiter => {
    // Route authentication rate limiting to an isolated storage
    limiter.AddPolicy("auth", policy => {
        policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(1))
              .UseKeyedStorage("security-redis")
              .WithFailOpen();
    });

    // Route billing rate limiting to a custom database backend
    limiter.AddPolicy("billing", policy => {
        policy.UseGcra(limit: 100, period: TimeSpan.FromHours(1))
              .UseStorage<PostgresCounterStorage>();
    });
});
```

---

## Resilience Decorators

Decorators can be chained onto any policy in the builder:

### `WithFailOpen()` (`ResilientRateLimiter`)
Catches storage and network exceptions thrown by the underlying storage backend and allows the request through with `RateLimitDecision.Allowed()`, logging an error to prevent API outages during infrastructure downtime. Cancellation exceptions are preserved and rethrown.

### `WithNegativeCaching()` (`NegativeCacheRateLimiter`)
Remembers denied keys in an in-memory `ConcurrentDictionary` for the duration of their `RetryAfter` window. Subsequent requests during the penalty period are rejected immediately in local RAM without making network round-trips to remote storage.

---

## Observability

### OpenTelemetry Metrics (`Wiaoj.RateLimiting`)

Subscribe to the meter in OpenTelemetry configuration:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => {
        metrics.AddMeter("Wiaoj.RateLimiting");
    });
```

- `ratelimit.decisions` (`Counter<long>`): Number of decisions evaluated, tagged with `algorithm` and `decision` (`allowed` / `denied`).
- `ratelimit.cost.consumed` (`Counter<long>`): Total units consumed by permitted requests.
- `ratelimit.queue.wait_duration` (`Histogram<double>` in `ms`): Time requests spent suspended in `LeakyBucketQueueRateLimiter`.

### Logging

Source-generated structured log messages are emitted under the `Wiaoj.RateLimiting` category for decision outcomes, queue delays, speculative rollbacks, and storage failure fallbacks.

---

## License

This project is licensed under the MIT License.