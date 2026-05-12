# Wiaoj.ObjectPool

A high-performance, thread-safe, and unified asynchronous/synchronous object pooling library for .NET.

`Wiaoj.ObjectPool` extends the capabilities of `Microsoft.Extensions.ObjectPool` by providing a **unified API surface** for both sync and async pools. It introduces **true asynchronous support**, **blocking/bounded pools**, **factory pattern integration**, and **zero-allocation leasing**. It is designed for high-throughput applications where garbage collection pressure must be minimized and resource management is critical.

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.ObjectPool.svg)](https://www.nuget.org/packages/Wiaoj.ObjectPool)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## 🚀 Key Features

- **⚡ True Async Support:** Native `IAsyncObjectPool<T>` with `ValueTask` support. Handles asynchronous creation (`CreateAsync`) and cleanup (`TryResetAsync`).
- **🤝 Unified API:** Provides a consistent, clean API surface for both `Microsoft`'s standard synchronous pools and our custom asynchronous pools. No need to switch contexts!
- **🏭 Factory Integration:** Seamlessly integrates with `IAsyncFactory<T>` for complex object creation logic using Dependency Injection.
- **🛡️ Hybrid Operation Modes:**
  - **FIFO (Elastic):** Lock-free, extremely fast. Creates new objects instantly if the pool is empty.
  - **Bounded (Blocking):** Uses `SemaphoreSlim`. Waits asynchronously if the pool limit is reached. Ideal for resource throttling (e.g., DB connections).
- **🧠 Smart Lifecycle Management:** Supports `IResettable` (Sync) and `IAsyncResettable` (Async) interfaces for self-managing objects.
- **📦 True Zero-Allocation Leasing:** Uses a value-type `struct` (`PooledObject<T>`) to manage the lease lifecycle, ensuring **0 heap allocations** during `Get/Return` cycles.

---

## 📥 Installation

Install via NuGet Package Manager:

```bash
dotnet add package Wiaoj.ObjectPool
```

---

## ⚡ Quick Start (Synchronous)

For simple CPU-bound objects like `StringBuilder` or `List<T>`, use the standard synchronous pool. Our library wraps it cleanly.

### 1. Register in DI
```csharp
using Wiaoj.ObjectPool.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Simple registration (uses new T())
builder.Services.AddObjectPool<StringBuilder>();

// Or with custom factory and reset logic via Lambdas
builder.Services.AddObjectPool<List<int>>(
    factory: () => new List<int>(),
    resetter: list => { list.Clear(); return true; }
);
```

### 2. Inject and Use
```csharp
public class StringService(IObjectPool<StringBuilder> pool)
{
    public string BuildMessage()
    {
        // 'using' ensures the object is automatically returned to the pool
        using PooledObject<StringBuilder> lease = pool.Lease();
        
        StringBuilder sb = lease.Item;
        sb.Append("Hello High Performance!");
        
        return sb.ToString();
    }
}
```

---

## 🔥 Advanced Usage (Asynchronous)

This is where `Wiaoj.ObjectPool` shines. Ideal for database connections, network streams, or any resource where creation/reset is costly and requires I/O.

### 1. Register Async Pool
```csharp
builder.Services.AddAsyncPool<MyDbConnection>(
    factory: async ct => await MyDbConnection.CreateAsync(ct),
    resetter: async conn => await conn.ResetStateAsync(),
    options => 
    {
        // Wait asynchronously if 50 connections are already in use.
        options.MaximumRetained = 50;
        options.AccessMode = PoolAccessMode.Bounded; 
    }
);
```

### 2. Inject and Use
```csharp
public class DataService(IAsyncObjectPool<MyDbConnection> dbPool)
{
    public async Task ProcessDataAsync()
    {
        // Leases a connection asynchronously. 
        // If pool is 'Bounded' and full, this line awaits until a slot opens.
        // Supports 'await using' for async disposal.
        await using var lease = await dbPool.LeaseAsync();
        
        var connection = lease.Item;
        await connection.ExecuteQueryAsync("SELECT * FROM Users");
    }
}
```

---

## 🧠 Defining Pool Logic: Strategies

`Wiaoj.ObjectPool` offers flexibility in how you define object creation and cleanup.

### 1. The "OOP" Way (IResettable) - **Recommended**
Best for objects that know how to clean themselves. No external logic required in `Program.cs`.

**Step 1:** Implement `IResettable` (Sync) or `IAsyncResettable` (Async).
```csharp
public class SocketClient : IAsyncResettable
{
    public async ValueTask<bool> TryResetAsync()
    {
        await SendResetCommandAsync(); // Async cleanup!
        return true;
    }
}
```

**Step 2:** Register
```csharp
// No factory or resetter needed! 
builder.Services.AddAsyncResettablePool<SocketClient>(); 
```

### 2. The "Factory" Way (IAsyncFactory)
Best when object creation is complex and already handled by an `IAsyncFactory<T>` implementation in your DI container.

```csharp
// 1. You already have a factory registered
builder.Services.AddSingleton<IAsyncFactory<MyService>, MyServiceFactory>();

// 2. Register the pool (It automatically resolves the factory!)
builder.Services.AddAsyncFactoryPool<MyService>(
    resetter: async svc => { /* Custom reset logic */ return true; }
);
```

---

## ⚙️ Configuration Modes

You can control the pool's behavior via `ObjectPoolOptions.AccessMode`:

| Mode | Description | Best For |
| :--- | :--- | :--- |
| **FIFO (Default)** | **Lock-Free / Elastic.** If the pool is empty, it immediately creates a new object. Limits only apply when returning to the pool. | CPU-bound objects (`StringBuilder`, buffers) where latency matters most. |
| **Bounded** | **Throttled / Blocking.** If the pool reaches the `MaximumRetained` limit, `LeaseAsync()` will **await** until an object is returned. | Limited resources (DB Connections, Throttled API Clients) to prevent system overload. |

---

## ⚠️ Best Practices: Zero-Allocation Leasing

To achieve `0` heap allocations during the `Lease` operation, `PooledObject<T>` is designed as a `struct` (Value Type). 

**Important Rule:** Do NOT copy the leased struct or pass it by value to other methods. Doing so could result in double-disposal. Always use it tightly within a `using` or `await using` block.

**Correct:**
```csharp
await using var lease = await pool.LeaseAsync();
var client = lease.Item;
```

**Incorrect:**
```csharp
var lease1 = pool.Lease();
var lease2 = lease1; // ❌ DO NOT DO THIS! (Copies the struct)
```

---

## 📄 License

This project is licensed under the MIT License.