# Wiaoj.RateLimiting

> High-performance, distributed, and RFC-compliant rate limiting infrastructure for modern .NET applications.

`Wiaoj.RateLimiting` is a modular, algorithm-agnostic rate limiting framework designed from the ground up to decouple rate limiting policies from underlying storage primitives and transport layers. It provides distributed cluster coordination via `Wiaoj.DistributedCounter`, built-in OpenTelemetry metrics, zero-allocation structured logging, and comprehensive ASP.NET Core middleware with full RFC 6585, RFC 9110, and RFC 7807/9457 compliance.

---

## 📑 Table of Contents

- [Key Highlights](#-key-highlights)
- [Architecture & Design Philosophy](#-architecture--design-philosophy)
- [Package Ecosystem](#-package-ecosystem)
- [Algorithm Matrix](#-algorithm-matrix)
- [Quick Start](#-quick-start)
  - [1. Core / Standalone Usage](#1-core--standalone-usage)
  - [2. ASP.NET Core Middleware](#2-aspnet-core-middleware)
- [Dynamic Cost & Bulk Rate Limiting](#-dynamic-cost--bulk-rate-limiting)
- [Observability (OpenTelemetry & Logging)](#-observability-opentelemetry--logging)
- [Standards & RFC Compliance](#-standards--rfc-compliance)
- [Zero-Allocation Design](#-zero-allocation-design)
- [License](#-license)

---

## ⚡ Key Highlights

- **Decoupled Primitives:** Rate limit decisions (`IsAllowed`, `RetryAfter`, `Remaining`) are strictly decoupled from counter storage mechanics.
- **Storage-Agnostic Distributed Engine:** Backed by `IDistributedCounter`, allowing seamless switching between high-performance in-memory CAS and distributed Redis storage with single-roundtrip Lua scripts.
- **7 Native Algorithms:** From cheap fixed windows to sliding weighted approximations, exact sorted logs, token buckets, GCRA, and queue-based traffic shapers.
- **RFC Compliance:**
  - **RFC 6585:** `429 Too Many Requests` status code.
  - **RFC 9110 (RFC 7231):** Integer delta-seconds `Retry-After` header.
  - **RFC 7807 / RFC 9457:** Standardized `application/problem+json` `ProblemDetails` responses.
  - **IETF RateLimit Draft:** `RateLimit-Remaining` and `RateLimit-Reset` headers.
- **Built-in OpenTelemetry:** Direct integration with `System.Diagnostics.Metrics` using stack-allocated `TagList` without external package bloat.
- **High-Throughput / Zero-Allocation:** Span-based IP formatting (`stackalloc char[48]`), compile-time source-generated logging (`[LoggerMessage]`), and minimal heap allocations.
- **Dynamic Endpoint Costing:** Native support for endpoint metadata, route conventions (`.WithRateLimitCost(17)`), and dynamic batch size resolvers for bulk operations.

---

## 🏛 Architecture & Design Philosophy

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Consumer Layers                               │
│       ASP.NET Core Middleware  │  Webhook Pipelines  │  Custom APIs    │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     Wiaoj.RateLimiting.Abstractions                     │
│        IRateLimitAlgorithm  │  RateLimitDecision  │  Key Selectors      │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
       ┌─────────────────────────────┴─────────────────────────────┐
       ▼                                                           ▼
┌───────────────────────────────┐               ┌───────────────────────────────┐
│  Distributed Algorithms       │               │  In-Memory Engines & Shapers  │
│  - FixedWindowRateLimiter     │               │  - TokenBucketRateLimiter     │
│  - SlidingWindowRateLimiter   │               │  - GcraRateLimiter            │
│    (Weighted Approximation)   │               │  - SlidingWindowLogRateLimiter│
└──────────────┬────────────────┘               │  - LeakyBucketQueueRateLimiter│
               │                                └───────────────────────────────┘
               ▼
┌───────────────────────────────┐
│   Wiaoj.DistributedCounter    │
│   - RedisCounterStorage (Lua) │
│   - InMemoryCounterStorage    │
└───────────────────────────────┘
```

### Core Principles:
1. **The Counter does not know about Rate Limiting:** `IDistributedCounter` remains a pure "Increment + TTL" primitive. All quota, window, and denial math lives strictly inside `IRateLimitAlgorithm`.
2. **Algorithm is Swappable, Not Storage:** Switching between Fixed Window, Sliding Window, or Token Bucket requires zero change to your call-site contracts.
3. **No Hidden Thread Starvation:** Algorithms are immediate meters by default. Asynchronous queueing is explicit via `LeakyBucketQueueRateLimiter`.

---

## 📦 Package Ecosystem

| Package | Description | Target |
| :--- | :--- | :--- |
| **`Wiaoj.RateLimiting`** | Core engine, all 7 algorithms, DI builder, and OpenTelemetry/Logging diagnostics. | `.NET 8.0+` |
| **`Wiaoj.RateLimiting.AspNetCore`** | ASP.NET Core middleware, zero-allocation key selectors, RFC headers, and endpoint conventions. | `.NET 8.0+` |
| **`Wiaoj.RateLimiting.Testing`** | Deterministic `InMemoryRateLimitAlgorithm` test double for consumer testing. | `.NET 8.0+` |

---

## 📊 Algorithm Matrix

| Algorithm | State Model | Storage Backing | Concurrency & Behavior | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Fixed Window** | Single counter + TTL | `IDistributedCounter` (Redis / Memory) | Atomic single round-trip. Potential burst at window seam. | Low/Medium precision, distributed APIs, cheap quotas. |
| **Sliding Window (Weighted)** | 2 adjacent counter windows | `IDistributedCounter` (Redis / Memory) | Speculative increment + weighted decay calculation. Mitigates boundary bursts. | Cloudflare-style high-traffic distributed APIs. |
| **Token Bucket** | Tokens count + Last refill timestamp | In-Memory (`ConcurrentDictionary`) | Absorbs immediate bursts up to capacity, refills steadily. | Burst-tolerant microservices, webhook delivery. |
| **Generic Cell Rate (GCRA)** | Single TAT (Theoretical Arrival Time) | In-Memory (Redis compatible) | Single scalar timestamp projection. Mathematically equivalent to Token Bucket. | Burst-tolerant distributed rate limiting with minimal state. |
| **Sliding Window Log** | Timestamped Log list | In-Memory (`List<LogEntry>`) | Exact sliding lookback. Zero boundary burst. Memory scales with request volume. | High-security endpoints (Login, Payment processing). |
| **Leaky Bucket (Meter)** | Usage level + Last leak timestamp | In-Memory (`ConcurrentDictionary`) | Continuous leak at fixed rate. Immediate decision (no wait). | Metering resource consumption. |
| **Leaky Bucket (Queue)** | TAT Projection + Backlog | In-Memory (`TimeProvider.Delay`) | **Traffic Shaper:** Delays admitted requests until their turn arrives. Immediate 429 only on queue overflow. | Outbound API throttling, database query smoothing. |

---

## 🚀 Quick Start

### 1. Core / Standalone Usage

Install the core package:
```bash
dotnet add package Wiaoj.RateLimiting
dotnet add package Wiaoj.DistributedCounter
```

Configure via Dependency Injection:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;

var services = new ServiceCollection();

// Configure counter backend (InMemory or Redis)
services.AddDistributedCounter(b => b.UseInMemory());

// Configure Rate Limiting algorithm
services.AddWiaojRateLimiting(rl => {
    rl.UseFixedWindow(limit: 100, window: TimeSpan.FromMinutes(1));
});

var serviceProvider = services.BuildServiceProvider();
var limiter = serviceProvider.GetRequiredService<IRateLimitAlgorithm>();

// Evaluate an operation
RateLimitDecision decision = await limiter.TryAcquireAsync("client_ip:192.168.1.1", cost: 1);

if (decision.IsAllowed) {
    Console.WriteLine($"Allowed! Remaining quota: {decision.Remaining}");
} else {
    Console.WriteLine($"Denied! Retry after: {decision.RetryAfter?.TotalSeconds}s");
}
```

---

### 2. ASP.NET Core Middleware

Install the ASP.NET Core package:
```bash
dotnet add package Wiaoj.RateLimiting.AspNetCore
```

Configure in `Program.cs`:
```csharp
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.AspNetCore.KeySelectors;

var builder = WebApplication.CreateBuilder(args);

// 1. Register DistributedCounter & Rate Limiter
builder.Services.AddDistributedCounter(b => b.UseInMemory());
builder.Services.AddWiaojRateLimiting(rl => {
    rl.UseSlidingWindow(limit: 60, window: TimeSpan.FromMinutes(1));
});

// 2. Configure ASP.NET Core Rate Limiting Options
builder.Services.AddWiaojAspNetCoreRateLimiting(options => {
    options.KeySelector = new ClientIpKeySelector(prefix: "ip:");
    options.UseProblemDetails = true; // Returns RFC 7807 JSON on 429
});

var app = builder.Build();

// 3. Enable Middleware in the Pipeline
app.UseWiaojRateLimiting();

app.MapGet("/api/items", () => Results.Ok(new[] { "Item1", "Item2" }));

app.Run();
```

---

## 🎯 Dynamic Cost & Bulk Rate Limiting

Endpoints can customize their quota consumption statically or dynamically based on request data:

```csharp
// 1. Static Cost (Consumes 5 units per call)
app.MapPost("/api/reports/generate", () => Results.Ok())
   .WithRateLimitCost(5);

// 2. Dynamic Bulk Costing (Deducts quota equal to the batch item count!)
app.MapPost("/api/orders/bulk", (int? count) => Results.Ok())
   .WithRateLimitCost(ctx => {
       if (ctx.Request.Query.TryGetValue("count", out var val) && int.TryParse(val, out int batchCount)) {
           return batchCount;
       }
       return 1; // Fallback cost
   });

// 3. Rate Limit Bypass (Exempt from all limits)
app.MapGet("/health", () => Results.Ok("Healthy"))
   .DisableRateLimiting();
```

---

## 📈 Observability (OpenTelemetry & Logging)

`Wiaoj.RateLimiting` exposes standard .NET runtime metrics (`System.Diagnostics.Metrics`) and structured, compile-time logging (`[LoggerMessage]`) out of the box with zero third-party dependencies.

### OpenTelemetry Setup:
Subscribe to the meter in your OpenTelemetry configuration:
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => {
        metrics.AddMeter("Wiaoj.RateLimiting");
    });
```

### Metrics Produced:
- `ratelimit.decisions` (`Counter<long>`): Total count of rate limit evaluations partitioned by `algorithm` and `decision` (`allowed` / `denied`).
- `ratelimit.cost.consumed` (`Counter<long>`): Total units consumed by permitted requests.
- `ratelimit.queue.wait_duration` (`Histogram<double>`): Duration in milliseconds requests were suspended in `LeakyBucketQueueRateLimiter`.

---

## 📜 Standards & RFC Compliance

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

## ⚡ Zero-Allocation Design

- **IP Key Formatting:** `ClientIpKeySelector` formats IPv4 and IPv6 addresses directly into stack memory (`stackalloc char[48]`) using `IPAddress.TryFormat` before composing the final key, avoiding intermediate string allocations.
- **Metric Emits:** Uses stack-allocated `System.Diagnostics.TagList` struct rather than heap array allocations (`KeyValuePair[]`) on hot decision paths.
- **Source-Generated Logging:** Log messages compile to static byte-template formatters via `[LoggerMessage]`, avoiding string formatting and parameter boxing when debug/trace logging is disabled.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.