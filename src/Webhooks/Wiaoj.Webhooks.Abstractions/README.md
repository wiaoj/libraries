# Wiaoj.Webhooks.Abstractions

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Pure contracts, zero-allocation value objects, polymorphic delivery outcomes, and abstraction layer for the **Wiaoj.Webhooks** ecosystem with **zero 3rd-party dependencies**.

---

## 📦 Features & Design Principles

- **Zero-Allocation Domain Value Objects:** All primary identifiers (`WebhookPartitionKey`, `WebhookEndpointId`, `WebhookJobId`, `IdempotencyKey`, and `WebhookSignature`) are strongly-typed, immutable `readonly record struct`s implementing .NET 10 zero-allocation span parsing:
  - `ISpanParsable<T>` & `IUtf8SpanParsable<T>`
  - `ISpanFormattable` & `IUtf8SpanFormattable`
- **High-Performance Dictionary Lookups:** Implements `IAlternateEqualityComparer<ReadOnlySpan<char>, T>` across all value objects, enabling zero-allocation lookups in maps and caches via `.GetAlternateLookup<ReadOnlySpan<char>>()`.
- **Closed Polymorphic Result Hierarchy:** `WebhookDeliveryResult` defines an exhaustive, Native AOT-compliant discriminated union (`Delivered`, `Deduplicated`, `TransientFailure`, `PermanentFailure`).
- **Pure Abstraction:** Zero external runtime dependencies. Built exclusively on modern BCL primitives and `Wiaoj.Primitives`.

---

## 📚 Key Contracts & Interfaces

### 1. Ingress & Dispatch
- **`IWebhookDispatcher`:** The primary entry point for scheduling and dispatching typed domain events with optional partition key routing.
- **`IWebhookEvent`:** Marker contract for dispatchable webhook domain events.
- **`IWebhookEventRegistry`:** Static metadata and runtime registry mapping CLR event types to wire-format event discriminator names (e.g. `order.created`).

### 2. Pipeline Execution & Delivery
- **`IWebhookTransport`:** High-throughput queuing abstraction (In-Memory Channel, Sharded Router, Kafka, RabbitMQ, Outbox).
- **`IWebhookDeliverer`:** Terminal execution step responsible for performing actual HTTP/gRPC transmission and returning a `WebhookDeliveryResult`.
- **`IWebhookMiddleware` / `WebhookDelegate`:** Extensible pipeline step contract for cross-cutting concerns (idempotency, signing, rate limiting, audit).
- **`IWebhookJobHandler`:** Consumer execution coordinator resolving endpoints, running pipelines, and updating persistent stores.

### 3. Concurrency, Security & Resilience
- **`IWebhookDeliveryLock`:** Execution lock contract providing FIFO message serialization per `WebhookPartitionKey`.
- **`IWebhookSigner`:** Cryptographic payload signing and constant-time signature verification contract (HMAC-SHA256, HMAC-SHA512).
- **`IWebhookRetryPolicy`:** Transient error evaluation and backoff delay calculation strategy.
- **`IWebhookEndpointResolver`:** Decoupled resolver mapping `WebhookEndpointId` to target URLs and protected secrets.

### 4. Persistence & State Management
- **`IWebhookStore`:** Storage abstraction managing job entities (`WebhookJobRecord`), distributed execution lease locks, attempt history, and stale in-flight recovery.
- **`IIdempotencyStore` & `IIdempotencyKeyGenerator`:** Sliding-window deduplication contracts preventing duplicate webhook transmissions and executions.

---

## 🧱 Core Domain Models

| Model | Type | Description |
|---|---|---|
| **`WebhookPartitionKey`** | `readonly record struct` | Strongly-typed partition key used for FIFO message ordering across queues and locks. |
| **`WebhookEndpointId`** | `readonly record struct` | Strongly-typed destination endpoint identifier. |
| **`WebhookJobId`** | `readonly record struct` | Time-ordered unique job identifier (UUIDv7-backed). |
| **`IdempotencyKey`** | `readonly record struct` | Deterministic deduplication key combining endpoint, event name, and SIMD-accelerated payload hash. |
| **`WebhookSignature`** | `readonly record struct` | Canonical `t={timestamp},v1={hash}` signature header value object. |
| **`WebhookDeliveryJob`** | `sealed record` | Immutable unit of work enqueued onto execution transports. |
| **`WebhookJobRecord`** | `sealed class` | Mutable persistent entity representing the full lifecycle state in stores and outbox tables. |
| **`WebhookDeliveryContext`** | `sealed class` | Pipeline context carrying pre-serialized payloads, headers, endpoint config, and attempt history. |
| **`WebhookDeliveryResult`** | `abstract record` | Closed result hierarchy (`Delivered`, `Deduplicated`, `TransientFailure`, `PermanentFailure`). |

---

## 🔗 Main Ecosystem Documentation

For complete architecture, inbound receiver engine, and transport documentation, see the [Main Webhooks README](../README.md).