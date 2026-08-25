# Wiaoj.DistributedCounter.Abstractions

Core contracts, interfaces, and value primitives for the `Wiaoj.DistributedCounter` library.

This package defines the public API surface, storage provider interfaces, and domain value objects required to consume or extend the distributed counter system without referencing concrete storage implementations or runtime hosting packages.

---

## Installation

```bash
dotnet add package Wiaoj.DistributedCounter.Abstractions
```

---

## Domain Primitives and Types

### `CounterKey` (`readonly record struct`)
- Validated key type ensuring consistent string representation across storage providers.
- Implements `ISpanParsable<CounterKey>`, `IUtf8SpanParsable<CounterKey>`, and `ISpanFormattable` to support span-based parsing and formatting without unnecessary allocations.
- Includes a dedicated `JsonConverter` (`CounterKeyJsonConverter`) for direct JSON property/value serialization.

### `CounterValue` (`readonly record struct`)
- Numerical wrapper around a `long` value.
- Implements generic math interfaces (`IComparisonOperators`, `IAdditionOperators`, `ISubtractionOperators`).
- Enforces `checked` arithmetic operators (`+`, `-`), throwing `OverflowException` on boundary overflows rather than wrapping silently.
- Explicit cast to `long` prevents unintentional type coercion; implicit conversion from `long` is supported.

### `CounterExpiry` (`readonly record struct`)
- Encapsulates expiration policies and sliding TTL windows.
- Supports `CounterExpiry.Infinite` (`null` duration) for persistent counters.
- Provides factory methods (`From`, `FromSeconds`, `FromMinutes`, `FromTicks`) with non-positive duration validation.

### `CounterLimitResult` (`readonly record struct`)
Returned by quota-evaluating operations (`TryIncrementAsync`, `TryDecrementAsync`):
- `IsAllowed` (`bool`): Whether the requested amount was within limit boundaries.
- `CurrentValue` (`long`): Current counter value after the operation (or unchanged value if rejected).
- `Remaining` (`long`): Remaining capacity until reaching the limit threshold.
- `Ttl` (`TimeSpan?`): Live remaining time-to-live of the sliding expiration window, if available from storage.

### `CounterValueCollection` (`readonly struct`)
- Read-only batch container returned by `IDistributedCounterService.GetValuesAsync`.
- Uses a reference-counted `DisposeGuard` to return rented `Dictionary<string, CounterValue>` buffers to the underlying object pool upon disposal.
- Safe after disposal: indexers and `TryGetValue` return `CounterValue.Zero` without throwing exceptions.

### `CounterStrategy` (`enum`)
- `Immediate`: Every operation is dispatched synchronously to storage.
- `Buffered`: Operations are accumulated in local memory and flushed in batches by background workers.

---

## Core Interfaces

| Interface | Role |
| :--- | :--- |
| **`IDistributedCounter`** | Core operations: `IncrementAsync`, `DecrementAsync`, `TryIncrementAsync`, `TryDecrementAsync`, `GetValueAsync`, `SetAsync`, and `ResetAsync`. |
| **`IDistributedCounter<TTag>`** | Open-generic DI contract providing tag-level categorization and scoped key resolution via `.ForKey(key)`. |
| **`ICounterStorage`** | Low-level storage provider contract implemented by storage backends (e.g. In-Memory, Redis). |
| **`IDistributedCounterFactory`** | Engine factory contract for creating and caching counter instances by name or tag. |
| **`IDistributedCounterService`** | System-level batch queries (`GetValuesAsync`), manual flush triggers (`FlushAllAsync`), and state resets (`ResetAllAsync`). |
| **`ICounterKeyBuilder`** | Contract for formatting names, tags, and dynamic keys into structured `CounterKey` strings. |

---

## Configuration Models

- **`DistributedCounterOptions`:** Root configuration model holding global prefixes (`GlobalKeyPrefix`), default strategy (`DefaultStrategy`), auto-flush intervals (`AutoFlushInterval`), and tag registrations.
- **`CounterConfiguration`:** Specific settings for a named counter or tag. Allows configuring dedicated strategies, storage types (`UseStorage<T>()`), keyed storage identifiers (`UseKeyedStorage(key)`), or factory delegates (`UseStorage(factory)`).

---

## Usage Examples

### Consuming `IDistributedCounter<TTag>` in a Service

```csharp
using Wiaoj.DistributedCounter;

public sealed class RateLimiterService(IDistributedCounter<RateLimitTag> rateLimiter) {

    public async Task<bool> CheckRequestAllowedAsync(string clientIp, CancellationToken cancellationToken) {
        CounterLimitResult result = await rateLimiter
            .ForKey(clientIp)
            .TryIncrementAsync(
                amount: 1,
                limit: 10,
                expiry: CounterExpiry.FromMinutes(1),
                cancellationToken: cancellationToken);

        return result.IsAllowed;
    }
}

public sealed class RateLimitTag;
```

---

### Implementing a Custom Storage Backend

```csharp
using Wiaoj.DistributedCounter;

public sealed class CustomStorage : ICounterStorage {

    public ValueTask<CounterValue> AtomicIncrementAsync(
        CounterKey key, 
        long amount, 
        CounterExpiry expiry, 
        CancellationToken cancellationToken) {
        // Storage-specific atomic increment implementation
        return new ValueTask<CounterValue>(new CounterValue(amount));
    }

    public ValueTask<CounterLimitResult> TryIncrementAsync(
        CounterKey key, 
        long amount, 
        long limit, 
        CounterExpiry expiry, 
        CancellationToken cancellationToken) {
        // Storage-specific limit evaluation implementation
        return new ValueTask<CounterLimitResult>(new CounterLimitResult(true, amount, limit - amount, null));
    }

    public ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask GetManyAsync(ReadOnlyMemory<CounterKey> keys, Memory<CounterValue> destination, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask SetAsync(CounterKey key, CounterValue value, CounterExpiry expiry, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken) => throw new NotImplementedException();
}
```

---

## License

This project is licensed under the MIT License.