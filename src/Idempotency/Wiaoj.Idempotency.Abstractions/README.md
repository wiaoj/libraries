# Wiaoj.Idempotency.Abstractions

The contract for recording "this has already been handled", so work that arrives twice runs once.

```bash
dotnet add package Wiaoj.Idempotency.Abstractions
```

## What is here

- **`IdempotencyKey`** — a validated key string, parsable and formattable (`ISpanParsable`, `IUtf8SpanParsable`, `ISpanFormattable`), with a JSON converter.
- **`IIdempotencyStore`** — the store contract:

| Member | Meaning |
| --- | --- |
| `TryMarkProcessedAsync(key, window)` | Claim the key: `true` for the first caller, `false` for every later one inside the window. **The operation everything else is built on.** |
| `ContainsAsync(key)` | Has this key been claimed and not expired? |
| `MarkProcessedAsync(key, window)` | Record the key unconditionally. |
| `RemoveAsync(key)` | Release a claim, for rolling back after a failure. |

The contract knows nothing about HTTP, webhooks or messaging: it is membership with a TTL.

**Claim before the work, not after.** `TryMarkProcessedAsync` is a check and a reservation in one step; checking with `ContainsAsync` and then recording leaves a window where two callers both see "not yet".

## Implementations

| Package | Store | Use it when |
| --- | --- | --- |
| [`Wiaoj.Idempotency`](../Wiaoj.Idempotency/README.md) | In-process dictionary | One process, and a restart losing claims is acceptable |
| [`Wiaoj.Idempotency.Redis`](../Wiaoj.Idempotency.Redis/README.md) | Redis `SET NX PX` | More than one replica, or claims must outlive a restart |
