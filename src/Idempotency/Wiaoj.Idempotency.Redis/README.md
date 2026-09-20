# Wiaoj.Idempotency.Redis

A distributed [`IIdempotencyStore`](../Wiaoj.Idempotency.Abstractions/README.md), so a claim outlives the process that made it and is shared by every replica.

```bash
dotnet add package Wiaoj.Idempotency.Redis
```

## Usage

```csharp
// Uses the IConnectionMultiplexer already registered, sharing one connection.
builder.Services.AddRedisIdempotencyStore();

// Or connect from here:
builder.Services.AddRedisIdempotencyStore("localhost:6379,abortConnect=false");

// Or with full control, including the key prefix:
builder.Services.AddRedisIdempotencyStore(options => {
    options.KeyPrefix = "orders:idemp:";   // default: "wiaoj:idemp:"
    options.Database = 3;
});

// Several Redis connections in one application:
builder.Services.AddKeyedRedisIdempotencyStore("claims-cluster");
```

## How a claim is stored

| Contract | Redis |
| --- | --- |
| `TryMarkProcessedAsync(key, window)` | `SET <prefix><key> 1 NX PX <window>` → `true` when it was stored |
| `ContainsAsync(key)` | `EXISTS` |
| `MarkProcessedAsync(key, window)` | `SET <prefix><key> 1 PX <window>` |
| `RemoveAsync(key)` | `DEL` |

- **One round trip, atomic in Redis:** `SET NX` stores the key only if it is absent, so exactly one caller wins. Nothing is buffered — a buffered claim is not a claim, since two replicas could both believe they won.
- **Membership, not a value:** the stored value is one byte and is never read.
- **Namespaced keys:** the prefix keeps a claim from colliding with a counter or a cache entry in a shared instance.

## While Redis is unavailable

Every operation throws `RedisConnectionException` **at once** instead of queueing the command until StackExchange.Redis' backlog timeout expires (5 s by default). A claim that could not be recorded is a fact the caller has to decide about, and waiting five seconds per message only turns an outage into a stall. Operations resume as soon as the multiplexer reconnects.

**The decision is yours:** whether an unavailable store means "run the work anyway" (risking a duplicate) or "fail and let the message be redelivered" depends on what the work does. The store does not choose for you.

Use `abortConnect=false` so the application starts while Redis is down.
