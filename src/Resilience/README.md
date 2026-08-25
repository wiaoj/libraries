# Wiaoj.Resilience

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

An ultra-high-throughput, zero-allocation, distributed resilience and fault-tolerance engine built for modern **.NET 10** cloud architectures.

Engineered with state-machine-backed circuit breakers, concurrency bulkheads, speculative hedging, timeout cancellation, and adaptive traffic shaping powered by `Wiaoj.DistributedCounter` for transparent in-memory and Redis cluster synchronization.

---

## 📑 Table of Contents

- [Architectural Overview](#-architectural-overview)
- [Key Features & Guarantees](#-key-features--guarantees)
- [Core Resilience Strategies](#-core-resilience-strategies)
  - [1. Circuit Breaker (Consecutive Failures & Sampling Window)](#1-circuit-breaker)
  - [2. Bulkhead (Concurrency Limiter & Resource Isolation)](#2-bulkhead-concurrency-limiter)
  - [3. Timeout & Cancellation Deadlines](#3-timeout--cancellation-deadlines)
  - [4. Hedging (Speculative Parallel Execution)](#4-hedging-speculative-parallel-execution)
  - [5. Fallback & Graceful Degradation](#5-fallback--graceful-degradation)
  - [6. Adaptive Concurrency (Latency-Driven Shaping)](#6-adaptive-concurrency)
- [Dual Invocation Models](#-dual-invocation-models)
  - [1. Zero-Allocation Result Pattern (`TryAcquireAsync`)](#1-zero-allocation-result-pattern)
  - [2. Idiomatic Delegate Wrapper (`ExecuteAsync`)](#2-idiomatic-delegate-wrapper)
- [Distributed Storage Integration (`Wiaoj.DistributedCounter`)](#-distributed-storage-integration)
- [Quick Start](#-quick-start)
- [Observability & OpenTelemetry](#-observability--opentelemetry)
- [Ecosystem Packages](#-ecosystem-packages)
- [License](#-license)

---

## 🏛 Architectural Overview

```mermaid
flowchart TD
    Req["Incoming Operation Request"] --> Decision{"ICircuitBreaker.TryAcquireAsync"}
    
    subgraph StateMachine["Circuit Breaker State Machine"]
        Decision -->|State: Closed| Run["Execute Target Operation"]
        Decision -->|State: Open| FastFail["Fast-Fail: Denied<br/><i>Zero Network / Socket I/O</i>"]
        Decision -->|State: Half-Open| Probe["Allow Single Trial Probe"]
    end
    
    Run --> Outcome{"Operation Result"}
    Outcome -->|Success| Success["OnSuccessAsync<br/><i>Reset Failure Counters</i>"]
    Outcome -->|Transient Failure 5xx / Timeout| Fail["OnFailureAsync<br/><i>Increment Failure Metric</i>"]
    
    Probe --> ProbeOutcome{"Probe Outcome"}
    ProbeOutcome -->|Success| Success
    ProbeOutcome -->|Failure| TripAgain["Trip to Open for Full Break Duration"]
    
    Fail --> Threshold{"Threshold Reached?"}
    Threshold -->|Yes| Trip["Trip to Open State<br/><i>Set Blocked Timestamp + TTL</i>"]
    Threshold -->|No| Continue["Continue Operation"]
```

---

## ⚡ Key Features & Guarantees

- **Single Engine, Dual Storage:** Built on top of `IDistributedCounterFactory`, running with zero dependencies in-memory (`InMemoryCounterStorage`) or seamlessly synchronized across Kubernetes pods via Redis (`RedisCounterStorage`).
- **Zero-Allocation Result Pattern:** `TryAcquireAsync` returns immutable `CircuitExecutionDecision` structs, eliminating expensive exception stack traces on hot execution paths and high-throughput pipelines.
- **Thundering Herd Protection:** Strict concurrency gating in `Half-Open` state permits exactly **one** trial probe request through to recovering targets while fast-failing concurrent traffic.
- **Error Classification Awareness:** Ignores permanent client errors (`4xx Bad Request`, `404 Not Found`), tripping only on true transient failures (`5xx Server Error`, socket drops, timeouts).
- **Comprehensive OpenTelemetry Instrumentation:** Out-of-the-box activity tracing, metrics counters, and duration histograms for Prometheus and Grafana.

---

## 🛡️ Core Resilience Strategies

| Strategy | Primary Purpose | Best For | Status |
|---|---|---|---|
| **`Circuit Breaker`** | Shields failing targets from cascading failure loops by fast-failing traffic. | Webhooks, downstream REST APIs, remote microservices. | Production |
| **`Bulkhead`** | Limits maximum concurrent in-flight executions per resource to prevent thread starvation. | Multi-tenant worker pools, shared connection pools. | Planned |
| **`Timeout`** | Enforces maximum allowable execution deadlines via `CancellationToken`. | Long-hanging socket calls, slow database queries. | Planned |
| **`Hedging`** | Dispatches speculative parallel backup requests when latency exceeds p95 thresholds. | Read-only idempotent replica queries. | Planned |
| **`Fallback`** | Provides graceful degradation by returning defaults or secondary action paths. | Cached responses, dead-letter recording. | Planned |
| **`Adaptive Concurrency`** | Dynamically throttles concurrency limits based on observed p99 round-trip latency. | Congestion-sensitive downstream servers. | Planned |

---

### 1. Circuit Breaker

Supports two distinct algorithmic strategies depending on workload characteristics:

#### A. Consecutive Failures Strategy
Trips to `Open` state after $N$ consecutive transient failures occur without an intervening success. Ideal for webhooks and background workers:

```csharp
var options = new CircuitBreakerOptions
{
    FailureThreshold = 5,
    BreakDuration = TimeSpan.FromMinutes(2)
};

var breaker = new ConsecutiveFailuresCircuitBreaker(counterFactory, options, timeProvider, logger);
```

#### B. Sampling Window (Failure Rate %) Strategy
Trips to `Open` when the failure rate exceeds a percentage threshold within a rolling lookback window, requiring a minimum request throughput. Ideal for high-throughput APIs:

```csharp
var options = new SamplingWindowCircuitBreakerOptions
{
    FailureRateThreshold = 0.5,        // 50% failure rate
    MinimumThroughput = 20,            // Minimum 20 requests in window
    SamplingWindow = TimeSpan.FromSeconds(30),
    BreakDuration = TimeSpan.FromMinutes(1)
};

var breaker = new SamplingWindowCircuitBreaker(counterFactory, options, timeProvider, logger);
```

---

## 🎮 Dual Invocation Models

### 1. Zero-Allocation Result Pattern
Designed for high-throughput pipelines, middleware, and performance-critical loops without exception overhead:

```csharp
CircuitExecutionDecision decision = await breaker.TryAcquireAsync("orders-api", cancellationToken);

if (!decision.IsAllowed)
{
    // Fast-fail path: Circuit is OPEN
    TimeSpan retryAfter = decision.RetryAfter ?? TimeSpan.FromMinutes(1);
    return Results.StatusCode(503);
}

try
{
    await ExecuteHttpRequestAsync();
    await breaker.OnSuccessAsync("orders-api", cancellationToken);
}
catch (Exception ex) when (IsTransient(ex))
{
    await breaker.OnFailureAsync("orders-api", cancellationToken);
    throw;
}
```

### 2. Idiomatic Delegate Wrapper
Executes an asynchronous operation delegate, automatically recording success or failure and throwing `CircuitBreakerOpenException` if open:

```csharp
string response = await breaker.ExecuteAsync("payment-service", async ct =>
{
    return await httpClient.GetStringAsync("https://api.payments.com/v1/status", ct);
}, cancellationToken);
```

---

## 🗄️ Distributed Storage Integration

`Wiaoj.Resilience` delegates counter state, time-to-live (TTL), and distributed locking to `Wiaoj.DistributedCounter`.

```csharp
// 1. Single-Node In-Memory Mode (Zero Network Overhead)
builder.Services.AddDistributedCounter(c => c.UseInMemory());

// 2. Multi-Node Cluster Mode (Shared Circuit State across Kubernetes Pods)
builder.Services.AddDistributedCounter(c => c.UseRedis("redis-cluster:6379"));
```

---

## 🚀 Quick Start

### 1. Register Resilience Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Register Distributed Counter backing
builder.Services.AddDistributedCounter(c => c.UseInMemory());

// Register Circuit Breaker
builder.Services.AddSingleton<ICircuitBreaker>(sp =>
{
    var factory = sp.GetRequiredService<IDistributedCounterFactory>();
    var timeProvider = sp.GetRequiredService<TimeProvider>();
    return new ConsecutiveFailuresCircuitBreaker(factory, new CircuitBreakerOptions
    {
        FailureThreshold = 5,
        BreakDuration = TimeSpan.FromMinutes(1)
    }, timeProvider);
});
```

---

## 📊 Observability & OpenTelemetry

- **ActivitySource:** `Wiaoj.Resilience`
  - Spans: `circuit_breaker.execute`, `circuit_breaker.probe`
  - Tags: `resilience.key`, `resilience.circuit_state`, `resilience.is_allowed`, `resilience.failure_count`
- **Meter Instruments:** `Wiaoj.Resilience`
  - `wiaoj.resilience.circuit_breaker.state` (UpDownCounter: 0 = Closed, 1 = Open, 2 = HalfOpen)
  - `wiaoj.resilience.circuit_breaker.tripped.count` (Counter)
  - `wiaoj.resilience.circuit_breaker.probe.count` (Counter)
  - `wiaoj.resilience.circuit_breaker.fast_fail.count` (Counter)

---

## 📦 Ecosystem Packages

| Package | Description | Reference Link |
|---|---|---|
| **`Wiaoj.DistributedCounter`** | High-performance atomic CAS counter and Redis Lua engine backing resilience state. | [README](../DistributedCounter/Wiaoj.DistributedCounter/README.md) |
| **`Wiaoj.RateLimiting`** | Comprehensive rate limiting algorithm suite (GCRA, Token Bucket, Sliding Window). | [README](../RateLimiting/Wiaoj.RateLimiting/README.md) |
| **`Wiaoj.Webhooks`** | Enterprise webhook delivery engine with sharded FIFO concurrency and SSRF defense. | [README](../Webhooks/Wiaoj.Webhooks/README.md) |
| **`Wiaoj.Webhooks.Resilience`** | Satellite adapter connecting `Wiaoj.Resilience` to the outbound webhook pipeline. | [README](../Webhooks/Wiaoj.Webhooks.Resilience/README.md) |

---

## 📄 License

This project is licensed under the [MIT License](../../LICENSE).