# Wiaoj.Webhooks

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Zero Allocation](https://img.shields.io/badge/Design-Zero--Allocation-brightgreen)](https://github.com/wiaoj/libraries)
[![Tests Passing](https://img.shields.io/badge/Unit%20Tests-200%20Passed-success)](https://github.com/wiaoj/libraries)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

An ultra-high-throughput, modular, resilient, and enterprise-grade Webhook delivery engine built from the ground up for modern **.NET 10** architectures.

Engineered with zero-allocation span parsers, constant-time cryptographic verification, striped lock concurrency, Bloom filter-based deduplication, distributed rate limiting, and open telemetry observability.

---

## 📑 Table of Contents

- [Architectural Overview](#-architectural-overview)
- [End-to-End Lifecycle Guide](./docs/ARCHITECTURE.md)
- [Ecosystem Packages](#-ecosystem-packages)
- [Key Features & Guarantees](#-key-features--guarantees)
- [Quick Start](#-quick-start)
- [Pipeline Lifecycle](#-pipeline-lifecycle)
- [Advanced Modules](#-advanced-modules)
  - [Partitioned Delivery (Striped Concurrency)](#1-partitioned-delivery-striped-concurrency)
  - [Bloom Filter Deduplication](#2-bloom-filter-deduplication)
  - [Distributed Rate Limiting](#3-distributed-rate-limiting)
  - [Cryptographic HMAC Signatures & Secret Rotation](#4-cryptographic-hmac-signatures--secret-rotation)
  - [Resilient Backoff & Retries](#5-resilient-backoff--retries)
- [Observability & OpenTelemetry](#-observability--opentelemetry)
- [Verification & Test Suite](#-verification--test-suite)

---

## 🏛 Architectural Overview

```mermaid
flowchart TD
    subgraph Ingress["1. Ingress & Dispatch"]
        A[Application Event] -->|dispatcher.DispatchAsync| B[IWebhookDispatcher]
        B --> C[(IWebhookTransport - Queue)]
    end

    subgraph Consumer["2. Background Consumer Loop"]
        C -->|Pulls WebhookDeliveryJob| D[WebhookJobHandler]
        D -->|Resolves Endpoint & Payload| E[WebhookPipelineRunner]
    end

    subgraph Pipeline["3. Extensible Outbound Middleware Pipeline"]
        E --> M1[PartitionedDeliveryMiddleware<br/><i>StripedLock&lt;EndpointId&gt;</i>]
        M1 --> M2[BloomFilterDeduplicationMiddleware<br/><i>O(1) Memory Idempotency</i>]
        M2 --> M3[DistributedRateLimitingMiddleware<br/><i>Sliding Window Counter</i>]
        M3 --> M4[SigningMiddleware<br/><i>HMAC-SHA256 / SHA512</i>]
        M4 --> M5[HttpWebhookDeliverer<br/><i>HTTP POST Execution</i>]
        M5 --> M6[RetryMiddleware<br/><i>Exponential / Linear Backoff</i>]
    end

    subgraph Egress["4. Target Delivery"]
        M5 -->|POST with Wiaoj-Signature| Target[Target Webhook Endpoint]
    end

    M6 -.->|On Transient Failure (5xx / 429)| C
```

---

## 📦 Ecosystem Packages

| Package | Description | Reference Link |
|---|---|---|
| **`Wiaoj.Webhooks.Abstractions`** | Pure contracts, value objects (`WebhookEndpointId`, `WebhookSignature`), and context definitions with zero 3rd-party dependencies. | [README](./Wiaoj.Webhooks.Abstractions/README.md) |
| **`Wiaoj.Webhooks`** | Core engine, middleware pipeline runner, HTTP delivery, HMAC signers, backoff policies, and OpenTelemetry instrumentation. | [README](./Wiaoj.Webhooks/README.md) |
| **`Wiaoj.Webhooks.Transports.InMemory`** | High-performance bounded channel transport and background consumer worker for single-node & local testing. | [README](./Wiaoj.Webhooks.Transports.InMemory/README.md) |
| **`Wiaoj.Webhooks.BloomFilter`** | O(1) duplicate webhook suppression plugin backed by `Wiaoj.BloomFilter` without database roundtrips. | [README](./Wiaoj.Webhooks.BloomFilter/README.md) |
| **`Wiaoj.Webhooks.DistributedCounter`** | Distributed per-endpoint rate limiting plugin backed by `Wiaoj.DistributedCounter` with automatic delayed re-queuing. | [README](./Wiaoj.Webhooks.DistributedCounter/README.md) |

---

## ⚡ Key Features & Guarantees

- **Modern .NET 10 Primitives**: Value objects (`WebhookEndpointId`, `WebhookSignature`) strictly implement `ISpanParsable<T>`, `IUtf8SpanParsable<T>`, `ISpanFormattable`, `IUtf8SpanFormattable`, and `IAlternateEqualityComparer<ReadOnlySpan<char>, T>` for allocation-free lookups.
- **Constant-Time Verification**: Cryptographically robust header parsing protected against timing attacks, token pollution, integer overflows, and comma-bomb DoS.
- **Partitioned Concurrency**: Endpoints are isolated across hash partitions (`StripedLock<WebhookEndpointId>`), guaranteeing strict FIFO message ordering per client while executing different clients with massive parallel throughput.
- **High-Performance Deduplication**: Instantaneous duplicate event filtering via vectorized bit-level Bloom filters.
- **Throttling & Backpressure**: Seamless integration with distributed counters to enforce rate limits and defer requests via scheduled retry delays.
- **Comprehensive Observability**: Full OpenTelemetry Activity tracing (`Wiaoj.Webhooks`) and Metrics (`Meter`) capturing attempt latencies, status codes, and failure distributions.

---

## 🚀 Quick Start

### 1. Register Webhook Services

```csharp
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.BloomFilter;
using Wiaoj.Webhooks.DistributedCounter;
using Wiaoj.Webhooks.Retries;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Supporting Services
builder.Services.AddDistributedCounter(dc => dc.UseInMemory());
builder.Services.AddBloomFilter(bf => bf.AddFilter("webhook-dedup", expectedItems: 100_000, errorRate: 0.001));
builder.Services.AddSingleton<IBloomFilter>(sp => sp.GetRequiredKeyedService<IBloomFilter>("webhook-dedup"));

// 2. Register Webhooks Engine in a Single Unified Builder Call
builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UseInMemoryTransport(capacity: 100_000)
            .UsePartitionedDelivery(stripes: 64)
            .UseBloomFilterDeduplication()
            .UseDistributedRateLimiting(maxRequestsPerWindow: 50, window: TimeSpan.FromSeconds(1))
            .UseHmacSha256Signing()
            .UseExponentialBackoffRetry(new ExponentialBackoffOptions
            {
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                Multiplier = 2.0,
                UseJitter = true
            });
});
```

### 2. Define and Dispatch an Event

```csharp
// Define Event
public sealed record OrderCompletedEvent(string OrderId, decimal Amount) : IWebhookEvent;

// Dispatch Asynchronously
public class OrderService(IWebhookDispatcher dispatcher)
{
    public async Task CompleteOrderAsync(string orderId, decimal amount, CancellationToken ct)
    {
        var @event = new OrderCompletedEvent(orderId, amount);
        await dispatcher.DispatchAsync(new WebhookEndpointId("customer-endpoint-1"), @event, ct);
    }
}
```

---

## ⚙️ Advanced Modules

### 1. Partitioned Delivery (Striped Concurrency)
Ensures that events targeting the same endpoint are processed sequentially (FIFO) to preserve state consistency, while events targeting different endpoints execute concurrently across CPU cores.

```csharp
builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UsePartitionedDelivery(stripes: 128);
});
```

### 2. Bloom Filter Deduplication
Intercepts identical events before HTTP transmission using an in-memory bit array. If a duplicate is detected, it short-circuits the pipeline with a successful 200 OK audit result.

```csharp
builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UseBloomFilterDeduplication(new BloomFilterDeduplicationOptions
    {
        KeySelector = ctx => $"{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload}"
    });
});
```

### 3. Distributed Rate Limiting
Enforces per-endpoint delivery rate limits across a cluster. When limits are exceeded, deliveries are rescheduled back to the queue with a sliding window delay.

```csharp
builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UseDistributedRateLimiting(maxRequestsPerWindow: 20, window: TimeSpan.FromSeconds(1));
});
```

### 4. Cryptographic HMAC Signatures & Secret Rotation
Signs outgoing payloads using canonical `t={timestamp},v1={hex}` headers with timestamp replay protection. Fully supports dual-secret rotation.

```csharp
// Sender Pipeline:
options.UseHmacSha256Signing();

// Receiver Verification:
bool isValid = signer.Verify(
    payloadBytes: requestBody,
    signatureHeader: request.Headers["Wiaoj-Signature"],
    secret: secretBytes,
    clockSkewTolerance: TimeSpan.FromMinutes(5)
);
```

### 5. Resilient Backoff & Retries
Classifies HTTP status codes and transparently reschedules transient errors (`408`, `429`, `500`, `502`, `503`, `504`) with jittered exponential backoff.

---

## 📊 Observability & OpenTelemetry

The engine emits rich telemetry out of the box:

- **Activity Source**: `Wiaoj.Webhooks`
  - Spans: `Webhook.Dispatch`, `Webhook.Deliver`, `Webhook.Pipeline`
  - Tags: `webhook.endpoint.id`, `webhook.status_code`, `webhook.attempt_number`
- **Meter Metrics**:
  - `webhooks.deliveries.total` (Counter)
  - `webhooks.delivery.duration` (Histogram in ms)
  - `webhooks.retries.total` (Counter)

---

## 🧪 Verification & Test Suite

All modules are strictly verified through **200+ unit and integration tests** covering edge cases, DoS defenses, clock skews, and concurrency stress tests:

```bash
dotnet test tests/Wiaoj.Webhooks.Tests.Unit/Wiaoj.Webhooks.Tests.Unit.csproj
```

```text
Passed!  - Failed: 0, Passed: 200, Skipped: 0, Total: 200, Duration: 537 ms
```

---

## 📄 License

This project is licensed under the [MIT License](../../LICENSE).
