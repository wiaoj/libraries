# Wiaoj.Mediator 🚀

**The High-Performance, Low-Allocation Mediator for .NET**

`Wiaoj.Mediator` is a lightweight implementation of the Mediator pattern, designed specifically for high-throughput enterprise applications. It outperforms traditional implementations by utilizing **Expression Tree Compilation**, **Frozen Dictionaries**, and **Zero-Runtime-Reflection** strategies.

---

## ⚡ Benchmarks: Wiaoj vs. MediatR

Benchmarks were performed on .NET 9.0. **Lower is better.**

| Method | Mean | Ratio | Allocated | Alloc Ratio |
| :--- | :---: | :---: | :---: | :---: |
| **Wiaoj: Send** | **30.63 ns** | **1.00** | **96 B** | **1.00** |
| MediatR: Send | 58.46 ns | 1.91 | 232 B | 2.42 |
| **Wiaoj: Stream** | **95.56 ns** | **1.00** | **104 B** | **1.00** |
| MediatR: Stream | 246.24 ns | 8.04 | 528 B | 5.50 |

### Why is it so fast?
1.  **Compile-Time Delegates:** Handlers and Pipelines are compiled into delegates using Expression Trees at startup. **No reflection `Invoke` is used during request handling.**
2.  **Frozen Architecture:** Handler lookups use `FrozenDictionary` for O(1) read speed.
3.  **Strategy Pattern:** Tracing and Observability logic is completely removed from the execution path if not enabled (No `if/else` checks at runtime).
4.  **Smart Caching:** Polymorphic dispatch (Base Class handlers) are cached efficiently using `ConcurrentDictionary`.

---

## 📦 Installation

```bash
dotnet add package Wiaoj.Mediator
```

---

## 🚀 Quick Start

### 1. Define Request & Handler
```csharp
// Request
public record Ping(string Message) : IRequest<string>;

// Handler
public class PingHandler : IRequestHandler<Ping, string> {
    public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken ) {
        return Task.FromResult($"Pong: {request.Message}");
    }
}
```

### 2. Register Services
```csharp
// Program.cs
builder.Services.AddMediator(cfg => {
    // Automatically scan assembly for Handlers
    cfg.RegisterHandlersFromAssemblyContaining<Program>();
    
    // Optional: Set default lifetime (Default is Scoped)
    cfg.WithDefaultLifetime(ServiceLifetime.Scoped);
});
```

### 3. Inject & Send
```csharp
public class MyController(IMediator mediator) : ControllerBase {
    [HttpGet]
    public async Task<IActionResult> Get() {
        var response = await mediator.Send(new Ping("Hello"));
        return Ok(response);
    }
}
```

---

## 🔥 Key Features

### 1. Granular Pipeline Configuration
Unlike other libraries, Wiaoj allows you to attach behaviors specifically to **Commands**, **Queries**, or **Streams**. This prevents logic leakage (e.g., Transaction behavior running on a Query).

```csharp
builder.Services.AddMediator(cfg => {
    // Runs for EVERYTHING
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));

    // Runs ONLY for ICommand<T>
    cfg.AddCommandBehavior(typeof(TransactionBehavior<,>));

    // Runs ONLY for IQuery<T>
    cfg.AddQueryBehavior(typeof(CachingBehavior<,>));
    
    // Safe registration (avoids duplicates)
    cfg.TryAddCommandBehavior<ValidationBehavior<,>>();
});
```

### 2. Stream Support (`IAsyncEnumerable`)
Native support for high-performance streaming without buffering.

```csharp
public record StreamData : IStreamRequest<int>;

public class StreamHandler : IStreamRequestHandler<StreamData, int> {
    public async IAsyncEnumerable<int> Handle(StreamData request, [EnumeratorCancellation] CancellationToken cancellationToken ) {
        for (int i = 0; i < 100; i++) {
            await Task.Delay(10, cancellationToken);
            yield return i;
        }
    }
}

// Usage
await foreach(var item in mediator.CreateStream(new StreamData())) {
    Console.WriteLine(item);
}
```

### 3. Polymorphic Dispatch
You can define a handler for a Base Class, and it will automatically handle requests for Derived Classes.

```csharp
public class BaseEvent : IRequest<Unit>;
public class UserCreated : BaseEvent;
public class OrderCreated : BaseEvent;

// This handler will process both UserCreated and OrderCreated
public class BaseEventHandler : IRequestHandler<BaseEvent, Unit> { ... }
```
*Wiaoj uses a smart caching mechanism to ensure sub-microsecond resolution after the first call.*

### 4. Zero-Overhead Observability (OpenTelemetry)
Wiaoj uses a **Strategy Pattern** for tracing.
*   **Default:** `Mediator` is registered. No Activity creation, no Overhead.
*   **Enabled:** `TracingMediator` is registered. Full OpenTelemetry support via `System.Diagnostics.ActivitySource`.

```csharp
builder.Services.AddMediator(cfg => {
    cfg.WithOpenTelemetry(); // Enables tracing
});
```

### 5. Compiled Exception Handling
Define global exception handlers without runtime reflection costs. The try/catch block is compiled directly into the pipeline delegate.

```csharp
public class GlobalErrorHandler : IRequestExceptionHandler<Ping, string, Exception> {
    public Task HandleAsync(Ping request, Exception ex, CancellationToken cancellationToken ) {
        Console.WriteLine($"Error: {ex.Message}");
        return Task.CompletedTask;
    }
}
```

---

## 🛠 Advanced Registration

The builder API provides a fluent and safe way to register components manually if you prefer not to use assembly scanning.

```csharp
builder.Services.AddMediator(cfg => {
    // Manual Handler Registration
    cfg.RegisterHandler<PingHandler>();
    
    // "Try" pattern to avoid duplicates in modular monoliths
    cfg.TryRegisterHandler<PongHandler>();
    
    // Lifetime management
    cfg.WithDefaultLifetime(ServiceLifetime.Transient);
});
```

---

## 📄 License

This project is licensed under the MIT License.

---

### ❤️ Acknowledgements
Inspired by the clean architecture of MediatR but re-engineered from the ground up for maximum throughput and minimal memory footprint in modern .NET ecosystems.