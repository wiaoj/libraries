# Wiaoj.Idempotency

The in-process [`IIdempotencyStore`](../Wiaoj.Idempotency.Abstractions/README.md): a concurrent dictionary of claims with TTL expiry.

```bash
dotnet add package Wiaoj.Idempotency
```

## Usage

```csharp
builder.Services.AddInMemoryIdempotencyStore();
```

```csharp
public sealed class OrderHandler(IIdempotencyStore claims) {
    public async Task HandleAsync(OrderPlaced message, CancellationToken ct) {
        IdempotencyKey key = new($"order-placed:{message.OrderId}");

        if(!await claims.TryMarkProcessedAsync(key, TimeSpan.FromHours(24), ct)) {
            return;   // someone already handled it
        }

        try {
            await DoTheWorkAsync(message, ct);
        }
        catch {
            await claims.RemoveAsync(key, ct);   // release the claim so a retry can run
            throw;
        }
    }
}
```

- **Atomic:** `TryMarkProcessedAsync` uses a compare-and-swap loop, so exactly one caller wins a key within one process.
- **Expiry:** an entry past its window counts as absent and is replaced in place; there is no background sweep.
- **Time:** it takes a `TimeProvider`, so tests control expiry instead of sleeping.

## When this store is not enough

Claims live in this process's memory, so they are **forgotten on restart** and **not shared between replicas** — which is exactly when a message is redelivered. Use [`Wiaoj.Idempotency.Redis`](../Wiaoj.Idempotency.Redis/README.md) for anything running more than one process, or a process that can restart mid-flight.
