# Wiaoj.Mediator 🚀

**The High-Performance, Low-Allocation Mediator for .NET**

`Wiaoj.Mediator` is a lightweight Mediator implementation engineered for high-throughput .NET applications.
It outperforms alternatives via **Expression Tree Compilation**, **Frozen Dictionaries**, and a layered pipeline architecture that adds zero overhead for disabled features.

---

## ⚡ Benchmarks: Wiaoj vs. MediatR

Benchmarks on .NET 9.0. **Lower is better.**

| Method          |    Mean |  Ratio | Allocated | Alloc Ratio |
|:----------------|--------:|-------:|----------:|------------:|
| **Wiaoj: Send** | **30.63 ns** | **1.00** | **96 B** | **1.00** |
| MediatR: Send   |  58.46 ns |   1.91 |    232 B  |        2.42 |
| **Wiaoj: Stream** | **95.56 ns** | **1.00** | **104 B** | **1.00** |
| MediatR: Stream |  246.24 ns |  8.04 |    528 B  |        5.50 |

---

## 📦 Installation

```bash
dotnet add package Wiaoj.Mediator

# Optional: compile-time handler discovery (zero startup reflection)
dotnet add package Wiaoj.Mediator.SourceGen
```

---

## 🚀 Quick Start

```csharp
// 1. Request + Handler
public record Ping(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<Ping, string> {
    public Task<string> HandleAsync(Ping request, CancellationToken ct)
        => Task.FromResult($"Pong: {request.Message}");
}

// 2. Registration
builder.Services.AddMediator(cfg => {
    cfg.RegisterHandlersFromAssemblyContaining<Program>();
});

// 3. Usage
var response = await mediator.Send(new Ping("Hello"));
```

---

## 🔥 Features

### 1. Granular Pipeline Behaviors

Attach behaviors specifically to Commands, Queries, or Streams to prevent logic leakage.

```csharp
builder.Services.AddMediator(cfg => {
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));          // all requests
    cfg.AddCommandBehavior(typeof(TransactionBehavior<,>));   // ICommand<T> only
    cfg.AddQueryBehavior(typeof(CachingBehavior<,>));         // IQuery<T> only
    cfg.TryAddCommandBehavior<ValidationBehavior<,>>();       // safe duplicate guard
});
```

---

### 2. Pre / Post Processors

Lightweight hooks that run before and after the handler — compiled into the pipeline at startup, zero runtime overhead.

```csharp
// Runs BEFORE the handler (validation, enrichment, idempotency)
public class OrderValidator : IRequestPreProcessor<CreateOrderCommand, OrderId> {
    public async Task ProcessAsync(CreateOrderCommand request, CancellationToken ct) {
        if (request.Quantity <= 0)
            throw new ValidationException("Quantity must be positive.");
    }
}

// Runs AFTER the handler (audit log, cache invalidation, event dispatch)
public class OrderAuditLogger : IRequestPostProcessor<CreateOrderCommand, OrderId> {
    public async Task ProcessAsync(CreateOrderCommand request, OrderId response, CancellationToken ct) {
        await _auditLog.WriteAsync($"Order {response} created at {DateTime.UtcNow}");
    }
}
```

No registration needed when using `RegisterHandlersFromAssemblyContaining` — processors are discovered automatically alongside handlers.

---

### 3. Built-in Behaviors (Attribute-Driven)

Apply retry, timeout, and rate limiting with a single attribute — no manual `AddOpenBehavior` call required.

```csharp
// Retry up to 3 times with exponential backoff
[Retry(count: 3, delayMs: 100, exponentialBackoff: true)]
public record CreateOrderCommand(...) : ICommand<OrderId>;

// Fail fast if handler takes longer than 500ms
[Timeout(milliseconds: 500)]
public record GetProductQuery(Guid Id) : IQuery<ProductDto>;

// Allow max 100 requests per minute (process-wide sliding window)
[RateLimit(maxRequests: 100, per: RateLimitWindow.Minute)]
public record SendEmailCommand(...) : ICommand<Unit>;

// Combine freely
[Retry(3), Timeout(800)]
public record SyncInventoryCommand() : ICommand<SyncResult>;
```

The behavior delegates are compiled as closed generics with static `Attribute` caching — **zero per-request overhead** when the attribute is absent.

---

### 4. Exception Handler with Recovery

Handle exceptions without losing the stack trace. Call `context.SetHandled(fallback)` to swallow the exception, or do nothing to let it propagate.

```csharp
public class GlobalErrorHandler : IRequestExceptionHandler<Ping, string, Exception> {
    public Task HandleAsync(
        Ping request,
        Exception exception,
        ExceptionContext<string> context,   // <-- new: recovery context
        CancellationToken ct) {

        _logger.LogError(exception, "Request {Name} failed", typeof(Ping).Name);

        // Option A: swallow and return a safe fallback
        context.SetHandled("Pong: fallback");

        // Option B: do nothing → exception is re-thrown (original stack trace preserved)
        return Task.CompletedTask;
    }
}
```

---

### 5. Stream Support

```csharp
public record StreamNumbers : IStreamRequest<int>;

public class StreamNumbersHandler : IStreamRequestHandler<StreamNumbers, int> {
    public async IAsyncEnumerable<int> Handle(
        StreamNumbers request,
        [EnumeratorCancellation] CancellationToken ct) {
        for (int i = 0; i < 100; i++) {
            await Task.Delay(10, ct);
            yield return i;
        }
    }
}

await foreach (var n in mediator.CreateStream(new StreamNumbers()))
    Console.WriteLine(n);
```

---

### 6. Polymorphic Dispatch

```csharp
public class BaseEvent : IRequest<Unit>;
public class UserCreated : BaseEvent;
public class OrderCreated : BaseEvent;

// Handles both UserCreated and OrderCreated — result cached after first call
public class BaseEventHandler : IRequestHandler<BaseEvent, Unit> { ... }
```

---

### 7. Zero-Overhead Observability (OpenTelemetry)

```csharp
builder.Services.AddMediator(cfg => {
    cfg.WithOpenTelemetry(); // swaps Mediator → TracingMediator at startup, zero if/else at runtime
});
```

---

### 8. Source Generator (Opt-In, Zero Startup Reflection)

Add `Wiaoj.Mediator.SourceGen` to eliminate `assembly.GetTypes()` scanning entirely.
All handler and processor registrations are emitted at compile time.

```csharp
// Add package: dotnet add package Wiaoj.Mediator.SourceGen

builder.Services.AddMediator(cfg => {
    // Generated method — replaces RegisterHandlersFromAssemblyContaining<T>()
    cfg.RegisterGeneratedHandlers();

    // Behaviors still registered manually (they are infrastructure, not domain)
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddCommandBehavior(typeof(TransactionBehavior<,>));
});
```

The generator emits `WiaojMediatorGenerated.g.cs` — one explicit `RegisterHandler` / `RegisterPreProcessor` / `RegisterPostProcessor` call per discovered type. No magic, fully inspectable.

---

## 🛠 Pipeline Execution Order

For a request with all features enabled:

```
Pre-Processors (in registration order)
    └─▶ Behaviors (outermost first, e.g. Logging → Transaction → Retry → Timeout)
            └─▶ Handler
    ◀─┘ Behaviors (unwinding)
    ◀── Exception Handler (if exception thrown)
Post-Processors (in registration order)
```

---

## 🛠 Advanced Registration

```csharp
builder.Services.AddMediator(cfg => {
    // Handler discovery
    cfg.RegisterHandlersFromAssemblyContaining<Program>();

    // Manual registration (modular monolith / library scenarios)
    cfg.RegisterHandler<PingHandler>();
    cfg.RegisterPreProcessor<AuditPreProcessor>();
    cfg.RegisterPostProcessor<CacheInvalidator>();

    // Safe "try" variants — no duplicates in multi-module setups
    cfg.TryRegisterHandler<PongHandler>();
    cfg.TryRegisterPreProcessor<AuditPreProcessor>();

    // Lifetime
    cfg.WithDefaultLifetime(ServiceLifetime.Transient);

    // Tracing
    cfg.WithOpenTelemetry();
});
```

---

## 📄 License

MIT License.

---

### ❤️ Acknowledgements

Inspired by MediatR's clean architecture — re-engineered from the ground up for maximum throughput,
minimal allocations, and a compile-time-first philosophy in modern .NET.