# Wiaoj.RateLimiting

A modular, policy-driven, and RFC-compliant rate limiting library for .NET applications.

`Wiaoj.RateLimiting` provides standalone in-memory algorithms, distributed rate limiting coordinated via `Wiaoj.DistributedCounter`, in-memory L1 negative caching, Fail-Open resilience, OpenTelemetry metrics, and ASP.NET Core middleware with full RFC 6585, RFC 9110, and RFC 7807/9457 compliance.

---

## Table of Contents

- [Key Highlights](#-key-highlights)
- [Architecture & Design Philosophy](#-architecture--design-philosophy)
- [Package Ecosystem](#-package-ecosystem)
- [Algorithm Matrix](#-algorithm-matrix)
- [Quick Start](#-quick-start)
  - [1. Core DI & Policy Setup](#1-core-di--policy-setup)
  - [2. ASP.NET Core Middleware](#2-aspnet-core-middleware)
- [Resilience & L1 Negative Caching](#-resilience--l1-negative-caching)
- [Dynamic Cost & Route Conventions](#-dynamic-cost--route-conventions)
- [Observability (OpenTelemetry & Logging)](#-observability-opentelemetry--logging)
- [Standards & RFC Compliance](#-standards--rfc-compliance)
- [Allocation Efficiency](#-allocation-efficiency)
- [License](#-license)

---

## Key Highlights

- **Policy-Driven Architecture:** Supports named policies (`IRateLimiter`) and strongly-typed marker tags (`IRateLimiter<TPolicy>`).
- **Distributed Engine Integration:** Seamlessly coordinates across cluster nodes via `Wiaoj.DistributedCounter` using single-roundtrip Redis Lua scripts and atomic CAS loops.
- **7 Built-in Algorithms:** Fixed window, sliding weighted window, GCRA, token bucket, sliding window log, leaky bucket queue (traffic shaping), and multi-tier composite limiter.
- **Built-in Resilience:** Optional `WithFailOpen()` (prevents API outages during storage failure) and `WithNegativeCaching()` (L1 RAM ban cache deflecting DDoS/spam).
- **RFC Compliance:**
  - **RFC 6585:** `429 Too Many Requests` status code.
  - **RFC 9110 (RFC 7231):** Integer delta-seconds `Retry-After` header.
  - **RFC 7807 / RFC 9457:** Standardized `application/problem+json` `ProblemDetails` responses.
  - **IETF RateLimit Draft:** `RateLimit-Remaining` and `RateLimit-Reset` headers.
- **Observability:** OpenTelemetry metrics (`System.Diagnostics.Metrics`) using stack-allocated `TagList` and compile-time source-generated logging (`[LoggerMessage]`).
- **Dynamic Endpoint Costing:** Fine-grained endpoint metadata, route conventions (`.WithRateLimitCost(5)`), and dynamic cost resolvers.

---

## Architecture & Design Philosophy

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Consumer Layers                               │
│       ASP.NET Core Middleware  │  Webhook Pipelines  │  Custom APIs    │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     Wiaoj.RateLimiting.Abstractions                     │
│    IRateLimiter  │  IRateLimiter<T>  │  IRateLimitAlgorithm  │ Decision │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
       ┌─────────────────────────────┴─────────────────────────────┐
       ▼                                                           ▼
┌───────────────────────────────┐               ┌───────────────────────────────┐
│  Distributed Algorithms       │               │  In-Memory Engines & Shapers  │
│  - FixedWindowRateLimiter     │               │  - TokenBucketRateLimiter     │
│  - SlidingWindowRateLimiter   │               │  - SlidingWindowLogRateLimiter│
│  - GcraRateLimiter (CAS)      │               │  - LeakyBucketQueueRateLimiter│
└──────────────┬────────────────┘               │  - CompositeRateLimiter       │
               │                                └───────────────────────────────┘
               ▼
┌───────────────────────────────┐
│   Wiaoj.DistributedCounter    │
│   - RedisCounterStorage (Lua) │
│   - InMemoryCounterStorage    │
└───────────────────────────────┘
```

---

## Package Ecosystem

| Package | Description | Target |
| :--- | :--- | :--- |
| **`Wiaoj.RateLimiting.Abstractions`** | Core contracts (`IRateLimiter`, `IRateLimitAlgorithm`, `RateLimitDecision`, `IRateLimitPolicyBuilder`). | `.NET 10.0+` |
| **`Wiaoj.RateLimiting`** | Runtime engine, all 7 algorithms, policy builder, resilience decorators, and diagnostics. | `.NET 10.0+` |
| **`Wiaoj.RateLimiting.AspNetCore`** | ASP.NET Core middleware, key selectors, RFC headers, endpoint conventions, and `ProblemDetails`. | `.NET 10.0+` |

---

## Algorithm Matrix

| Algorithm | State Model | Storage Backing | Concurrency & Behavior | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Fixed Window** | Single counter + TTL | `IDistributedCounter` (Redis / Memory) | Atomic single round-trip. Potential burst at window boundary. | General API protection, cheap distributed quotas. |
| **Sliding Window (Weighted)** | 2 adjacent counter windows | `IDistributedCounter` (Redis / Memory) | Blends previous window count with current window. Mitigates boundary bursts. | High-traffic distributed APIs. |
| **GCRA** | Single TAT timestamp | `IDistributedCounter` (Redis / Memory) | Optimistic CAS loop. Burst-tolerant single-scalar state. | Distributed burst-tolerant rate limiting. |
| **Token Bucket** | Tokens + Refill timestamp | In-Memory (`ConcurrentDictionary`) | Absorbs immediate bursts up to capacity, refills at constant rate. | In-memory burst-tolerant services. |
| **Sliding Window Log** | Timestamped Log list | In-Memory (`List<LogEntry>`) | Exact sliding lookback. Zero boundary burst. Memory scales with request count. | High-security endpoints (Login, Payment processing). |
| **Leaky Bucket (Queue)** | TAT Projection + Backlog | In-Memory (`TimeProvider.Delay`) | **Traffic Shaper:** Delays admitted requests to smooth traffic. 429 only on queue overflow. | Outbound webhook throttling, database query smoothing. |
| **Composite** | Sequence of algorithms | Any (Mixed) | Evaluates multiple tiers in order (e.g. 10 req/sec AND 1,000 req/day). | Multi-tier enterprise rate limiting. |

---

## Quick Start

### 1. Core DI & Policy Setup

Install the packages:
```bash
dotnet add package Wiaoj.RateLimiting
dotnet add package Wiaoj.DistributedCounter
```

Configure policies in `Program.cs`:
```csharp
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure storage backend (In-Memory or Redis)
builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

// 2. Configure rate limiting policies
builder.Services.AddWiaojRateLimiting(limiter => {
    // Named policy: Fixed window for auth
    limiter.AddPolicy("auth", policy => {
        policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(1))
              .WithFailOpen();
    });

    // Typed policy for orders
    limiter.AddPolicy<OrderPolicy>(policy => {
        policy.UseSlidingWindow(limit: 100, window: TimeSpan.FromMinutes(1));
    });

    // Default fallback policy
    limiter.UseDefaultPolicy(policy => {
        policy.UseFixedWindow(limit: 1000, window: TimeSpan.FromHours(1));
    });
});

var app = builder.Build();

// Direct consumption in services
public sealed class LoginService(IRateLimiter rateLimiter) {
    public async Task<IResult> LoginAsync(string clientIp, CancellationToken ct) {
        RateLimitDecision decision = await rateLimiter.TryAcquireAsync("auth", clientIp, ct);
        if (!decision.IsAllowed) {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
        return Results.Ok();
    }
}

public sealed class OrderPolicy;
```

---

### 2. ASP.NET Core Middleware

Install the ASP.NET Core package:
```bash
dotnet add package Wiaoj.RateLimiting.AspNetCore
```

Configure middleware in `Program.cs`:
```csharp
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

builder.Services.AddWiaojRateLimiting(limiter => {
    limiter.AddPolicy("api", policy => policy.UseSlidingWindow(60, TimeSpan.FromMinutes(1)));

    // Configure ASP.NET Core middleware options
    limiter.WithAspNetCore(options => {
        options.KeySelector = new ClientIpKeySelector(prefix: "ip:");
        options.UseProblemDetails = true;
    });
});

var app = builder.Build();

// Enable rate limiting in pipeline
app.UseWiaojRateLimiting();

app.MapGet("/api/items", () => Results.Ok(new[] { "Item1", "Item2" }))
   .RequireRateLimiting("api");

app.Run();
```

---

## Per-Policy Storage Routing

By default, distributed algorithms (`FixedWindow`, `SlidingWindow`, `Gcra`) route counter operations to the global storage configured in `AddDistributedCounter`. 

Specific policies can be routed to dedicated storage instances, keyed Redis multiplexers, or custom database backends directly within the policy builder:

```csharp
// Register multiple Redis multiplexers in DI
builder.Services.AddKeyedSingleton<IConnectionMultiplexer>(
    "security-cluster", 
    (_, _) => ConnectionMultiplexer.Connect("security-redis:6379"));

builder.Services.AddDistributedCounter(dc => {
    // Default fallback storage
    dc.UseRedis("main-redis:6379");
});

builder.Services.AddWiaojRateLimiting(limiter => {
    // 1. Critical authentication policy routed to an isolated Redis cluster via keyed storage
    limiter.AddPolicy("auth", policy => {
        policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(1))
              .UseKeyedStorage("security-cluster")
              .WithFailOpen();
    });

    // 2. Billing policy routed to a custom storage implementation
    limiter.AddPolicy("billing", policy => {
        policy.UseGcra(limit: 100, period: TimeSpan.FromHours(1))
              .UseStorage<PostgresCounterStorage>();
    });

    // 3. General API policy using default storage (main-redis)
    limiter.AddPolicy("general_api", policy => {
        policy.UseSlidingWindow(limit: 100, window: TimeSpan.FromMinutes(1));
    });
});
```

---

## Resilience & L1 Negative Caching

Policies support chaining resilience decorators directly in the fluent builder:

```csharp
limiter.AddPolicy("protected_api", policy => {
    policy.UseFixedWindow(limit: 10, window: TimeSpan.FromMinutes(1))
          // 1. L1 RAM negative cache: Deflects repeated spam during retry window without storage calls
          .WithNegativeCaching()
          // 2. Fail-Open: Allows request if remote storage (Redis) throws a network timeout
          .WithFailOpen();
});
```

---

## Dynamic Cost & Route Conventions

Endpoints can customize their quota consumption statically or dynamically based on request data:

```csharp
// 1. Static Cost (Consumes 5 units per call)
app.MapPost("/api/reports", () => Results.Ok())
   .WithRateLimitCost(5);

// 2. Dynamic Bulk Costing (Deducts quota equal to the batch item count)
app.MapPost("/api/orders/bulk", (int count) => Results.Ok())
   .WithRateLimitCost(ctx => {
       if (ctx.Request.Query.TryGetValue("count", out var val) && int.TryParse(val, out int batchCount)) {
           return Math.Max(1, batchCount);
       }
       return 1;
   });

// 3. Rate Limit Bypass (Exempt from all limits)
app.MapGet("/health", () => Results.Ok("Healthy"))
   .DisableRateLimiting();
```

---

## Observability (OpenTelemetry & Logging)

`Wiaoj.RateLimiting` exports standard .NET runtime metrics (`System.Diagnostics.Metrics`) and structured, compile-time logging (`[LoggerMessage]`):

### OpenTelemetry Metrics (`Wiaoj.RateLimiting`):
- `ratelimit.decisions` (`Counter<long>`): Total count of rate limit evaluations partitioned by `algorithm` and `decision` (`allowed` / `denied`).
- `ratelimit.cost.consumed` (`Counter<long>`): Total units consumed by permitted requests.
- `ratelimit.queue.wait_duration` (`Histogram<double>` in `ms`): Duration requests waited in `LeakyBucketQueueRateLimiter`.

---

## Standards & RFC Compliance

When a request exceeds quota, `Wiaoj.RateLimiting.AspNetCore` emits standard RFC responses:

```http
HTTP/1.1 429 Too Many Requests
Content-Type: application/problem+json
Retry-After: 12
RateLimit-Remaining: 0
RateLimit-Reset: 12

{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Quota will be available in 12 seconds.",
  "instance": "/api/orders/bulk",
  "retryAfter": 12,
  "remaining": 0
}
```

---

## Allocation Efficiency

- **Stack-Allocated IP Formatting:** `ClientIpKeySelector` formats IPv4 and IPv6 addresses into stack memory (`stackalloc char[48]`) via `IPAddress.TryFormat` before composing the final key, avoiding intermediate substring allocations.
- **Metric Emits:** Uses stack-allocated `System.Diagnostics.TagList` struct rather than heap array allocations (`KeyValuePair[]`) on hot decision paths.
- **Source-Generated Logging:** Log messages compile to static byte-template formatters via `[LoggerMessage]`, avoiding string formatting and parameter boxing when logging levels are disabled.

---

## License

This project is licensed under the MIT License.