# Wiaoj.Resilience

Distributed circuit breaker and timeout primitives for the `Wiaoj` library family.

Circuit state is held in `Wiaoj.DistributedCounter`, so a circuit tripped by one node is observed by every node sharing the same storage backend. The package ships the breaker algorithms, policy registration builders, execution helpers with fallback degradation, and OpenTelemetry metrics and tracing.

---

## Installation

```bash
dotnet add package Wiaoj.Resilience
```

---

## Circuit Breaker Strategies

### 1. `ConsecutiveFailuresCircuitBreaker` (Distributed / Storage-Backed)
- Trips once `FailureThreshold` consecutive failures are recorded; a success resets the failure counter.
- Half-open recovery admits exactly one trial probe, claimed atomically via `IDistributedCounter.TryIncrementAsync` so that only one caller across the cluster tests the target.

### 2. `SamplingWindowCircuitBreaker` (Distributed / Storage-Backed)
- Trips on the failure *rate* over a rolling window, evaluated only once `MinimumThroughput` requests have been observed within that window — a handful of failures during a quiet period will not trip it.
- Half-open recovery admits up to `PermittedNumberOfCallsInHalfOpenState` concurrent probes.

### 3. `CompositeCircuitBreaker`
- Evaluates an ordered sequence of breaker tiers. Execution is permitted only if every tier allows it; the first denying tier short-circuits the rest and its `RetryAfter` is surfaced.
- Useful for pairing a fast consecutive-failure trip with a slower percentage-based trip over a longer window.

All three implement `ICircuitBreaker`, which is transport-agnostic: the `key` identifies whatever you are protecting — an HTTP host, a tenant, a queue, a database shard.

---

## The Three States

| State | Meaning |
| --- | --- |
| `Closed` | Operational. Requests proceed normally. |
| `Open` | Tripped. Requests are fast-failed until `BreakDuration` elapses. |
| `HalfOpen` | The break elapsed. A bounded number of trial probes are admitted to test recovery; their outcome closes or re-opens the circuit. |

---

## Dependency Injection Setup

```csharp
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure the underlying counter backend that holds circuit state.
builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

// 2. Configure resilience policies.
builder.Services.AddWiaojResilience(resilience => {
    // Named policy: trip after 5 consecutive failures.
    resilience.AddConsecutiveBreaker("payments", options => {
        options.FailureThreshold = 5;
        options.BreakDuration = TimeSpan.FromMinutes(1);
    });

    // Named policy: trip when 50% of at least 20 requests fail within 30s.
    resilience.AddSamplingBreaker("search", options => {
        options.FailureRateThreshold = 0.5;
        options.MinimumThroughput = 20;
        options.SamplingWindow = TimeSpan.FromSeconds(30);
        options.BreakDuration = TimeSpan.FromMinutes(1);
        options.PermittedNumberOfCallsInHalfOpenState = 3;
    });

    // Multi-tier: both tiers must allow execution.
    resilience.AddCompositeBreaker("gateway", "payments", "search");

    // Strongly-typed policy, keyed by the marker type's name.
    resilience.AddConsecutiveBreaker<ShippingPolicy>(options => {
        options.FailureThreshold = 3;
    });

    // Default fallback policy.
    resilience.UseDefaultConsecutiveBreaker(options => {
        options.FailureThreshold = 5;
        options.BreakDuration = TimeSpan.FromSeconds(30);
    });

    // Timeout policies.
    resilience.AddFixedTimeout("payments", TimeSpan.FromSeconds(3));
    resilience.UseDefaultFixedTimeout(TimeSpan.FromSeconds(10));
});
```

---

## Executing Through a Circuit

Resolve a named policy through `ICircuitBreakerFactory`, or inject `ICircuitBreaker<TPolicy>` for a typed one.

```csharp
public sealed class PaymentClient(ICircuitBreakerFactory factory, HttpClient http) {
    private readonly ICircuitBreaker _breaker = factory.Create("payments");

    public ValueTask<Receipt> ChargeAsync(Order order, CancellationToken ct) {
        // Records success or failure automatically.
        // Throws CircuitBreakerOpenException (carrying Key and RetryAfter) when blocked.
        return this._breaker.ExecuteAsync(
            key: "payments-gateway",
            operation: token => this.PostChargeAsync(order, token),
            cancellationToken: ct);
    }
}
```

### Graceful Degradation

`ExecuteWithFallbackAsync` swallows the failure — including the open-circuit rejection — and returns a substitute instead, either a static value or a factory that receives the triggering exception. Caller cancellation is always rethrown rather than falling back.

```csharp
IReadOnlyList<Product> results = await breaker.ExecuteWithFallbackAsync(
    key: "search-cluster",
    operation: token => this._search.QueryAsync(term, token),
    fallbackFactory: (exception, token) => this._cache.LastKnownGoodAsync(term, token),
    cancellationToken: ct);
```

### Manual Outcome Reporting

When the operation is not a single delegate, drive the breaker directly:

```csharp
CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, ct);

if(!decision.IsAllowed) {
    return TooManyRequests(retryAfter: decision.RetryAfter);
}

try {
    await DispatchAsync(ct);
    await breaker.OnSuccessAsync(key, ct);
}
catch {
    await breaker.OnFailureAsync(key, ct);
    throw;
}
```

---

## Reading State Without Acquiring

`TryAcquireAsync` is not a way to *look* at a circuit: in the half-open state it hands out the probe that decides whether the target has recovered. `GetStateAsync` makes no state transition and consumes no probe, which is what pre-emptive routing needs.

```csharp
// Prefer a provider whose circuit is closed.
foreach(GatewayDescriptor candidate in candidates) {
    if(await breaker.GetStateAsync(candidate.Key, ct) is CircuitState.Closed) {
        return candidate;
    }
}
```

The result is **advisory and may be stale** — state lives in a distributed counter and can change between the read and a subsequent acquire. A `Closed` result is not a guarantee that the next `TryAcquireAsync` will be permitted, so callers must still handle a denial at acquire time.

---

## Timeouts

`ITimeoutStrategy` bounds an operation within a deadline, throwing `TimeoutException` when it is exceeded while leaving caller cancellation distinguishable as `OperationCanceledException`.

```csharp
public sealed class ReportService(ITimeoutStrategy<ReportPolicy> timeout) {
    public ValueTask<Report> BuildAsync(CancellationToken ct) {
        return timeout.ExecuteAsync("report-build", token => this._builder.RunAsync(token), ct);
    }
}
```

Named strategies are resolved through `ITimeoutStrategyFactory`, and `ExecuteWithFallbackAsync` overloads mirror the circuit breaker ones.

---

## Configuration Reference

### `CircuitBreakerOptions` (consecutive failures)

| Property | Default | Description |
| --- | --- | --- |
| `FailureThreshold` | `5` | Consecutive failures required to trip the circuit. |
| `BreakDuration` | `1 minute` | How long the circuit stays open before half-open probing. |
| `KeyPrefix` | `wiaoj:resilience:cb:` | Storage key prefix for isolation. |

### `SamplingWindowCircuitBreakerOptions` (failure rate)

| Property | Default | Description |
| --- | --- | --- |
| `FailureRateThreshold` | `0.5` | Failure ratio (0.0–1.0) required to trip. |
| `MinimumThroughput` | `10` | Minimum requests in the window before the rate is evaluated. |
| `SamplingWindow` | `30 seconds` | Rolling window across which the rate is calculated. |
| `BreakDuration` | `1 minute` | How long the circuit stays open before half-open probing. |
| `PermittedNumberOfCallsInHalfOpenState` | `5` | Concurrent trial probes admitted during recovery. |
| `KeyPrefix` | `wiaoj:resilience:cb:` | Storage key prefix for isolation. |

Both option types are validated on registration and implement `IDeepCloneable<T>` and `IMergeable<T>`.

---

## Observability

Metrics are emitted on the `Wiaoj.Resilience` meter and spans on the `Wiaoj.Resilience` activity source.

| Instrument | Kind | Description |
| --- | --- | --- |
| `circuit_breaker.decisions` | Counter | Acquire decisions, tagged by outcome and state. |
| `circuit_breaker.trips` | Counter | Transitions into the open state, tagged by reason. |
| `circuit_breaker.successes` | Counter | Recorded successes, flagging recoveries. |
| `circuit_breaker.failures` | Counter | Recorded failures. |
| `circuit_breaker.state` | Observable gauge | Current state per key. |

Spans are emitted on the `Wiaoj.Resilience` activity source for the delegate-wrapper execution model:

| Span | Emitted by | Tags |
| --- | --- | --- |
| `circuit_breaker.execute` | `ExecuteAsync` / `ExecuteWithFallbackAsync` | `resilience.key`, `resilience.circuit_state`, `resilience.outcome`, `resilience.probe` (half-open only), `resilience.retry_after_ms` (denied only) |
| `timeout.execute` | `ITimeoutStrategy.ExecuteAsync` | `resilience.key`, `resilience.timeout_ms`, `resilience.outcome` |

`resilience.outcome` is one of `success`, `failure`, `denied`, `timeout` or `cancelled`; the span carries an `Error` status (and the recorded exception) for the first four. The zero-allocation `TryAcquireAsync` path emits metrics but no span, and `StartActivity` returns `null` when nothing is subscribed, so tracing costs nothing until a listener is attached.

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("Wiaoj.Resilience"))
    .WithTracing(tracing => tracing.AddSource("Wiaoj.Resilience"));
```

---

## License

MIT
