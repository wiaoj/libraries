# Wiaoj.ObjectPool

A high-performance, thread-safe, and fully asynchronous object pooling library for .NET.

`Wiaoj.ObjectPool` extends the capabilities of `Microsoft.Extensions.ObjectPool` by adding **true asynchronous support**, **blocking/bounded pools**, **factory pattern integration**, **leak detection**, and **zero-allocation leasing**. It is designed for high-throughput applications where garbage collection pressure must be minimized and resource management is critical.

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.ObjectPool.svg)](https://www.nuget.org/packages/Wiaoj.ObjectPool)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## 🚀 Key Features

- **⚡ True Async Support:** Native `IAsyncObjectPool<T>` with `ValueTask` support. Handles asynchronous creation (`CreateAsync`) and cleanup (`TryResetAsync`).
- **🏭 Factory Integration:** Seamlessly integrates with `IAsyncFactory<T>` for complex object creation logic using Dependency Injection.
- **🛡️ Hybrid Operation Modes:**
  - **FIFO (Elastic):** Lock-free, extremely fast. Creates new objects instantly if the pool is empty.
  - **Bounded (Blocking):** Uses `SemaphoreSlim`. Waits asynchronously if the pool limit is reached. Ideal for resource throttling (e.g., DB connections).
- **🧠 Smart Lifecycle Management:** Supports `IResettable` (Sync) and `IAsyncResettable` (Async) interfaces for self-managing objects.
- **🔍 Leak Detection (Debug Only):** Automatically detects if a leased object is garbage collected without being returned, pinpointing the exact stack trace of the leak.
- **✅ Return Validation:** Optional validation logic to ensure objects are returned to the pool in a clean/valid state.
- **📦 Zero-Allocation Leasing:** Uses a `readonly struct` (`PooledObject<T>`) to manage the lease lifecycle, ensuring 0 heap allocations during `Get/Return` cycles.

---

## 📥 Installation

Install via NuGet Package Manager:

```bash
dotnet add package Wiaoj.ObjectPool
```

---

## ⚡ Quick Start (Synchronous)

For simple CPU-bound objects like `StringBuilder` or `List<T>`, use the standard synchronous pool.

### 1. Register in DI
```csharp
// Program.cs
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

This is where `Wiaoj.ObjectPool` shines. Ideal for database connections, network streams, or any resource where creation/reset is costly and async.

### 1. Register Async Pool
```csharp
builder.Services.AddAsyncObjectPool<MyDbConnection>(
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

## 🧠 Defining Pool Logic: 4 Strategies

`Wiaoj.ObjectPool` offers flexibility in how you define object creation and cleanup.

### 1. The "Quick" Way (Lambdas)
Great for simple objects where logic fits in one line.

```csharp
builder.Services.AddObjectPool<User>(
    factory: () => new User(),
    resetter: user => { user.Name = null; return true; }
);
```

### 2. The "OOP" Way (IResettable) - **Recommended**
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
builder.Services.AddAsyncResettableObjectPool<SocketClient>(); 
```

### 3. The "Factory" Way (IAsyncFactory)
Best when object creation is complex and already handled by an `IAsyncFactory<T>` implementation in your DI container.

```csharp
// 1. You already have a factory registered
builder.Services.AddSingleton<IAsyncFactory<MyService>, MyServiceFactory>();

// 2. Register the pool (It automatically uses the registered factory!)
builder.Services.AddAsyncObjectPoolFromFactory<MyService>(
    resetter: async svc => await svc.ResetAsync()
);
```

### 4. The "Full Control" Way (Custom Policy)
Best for complex dependencies where you need full control over the policy class.

```csharp
// Register a class implementing IPoolPolicy<T> or IAsyncPoolPolicy<T>
builder.Services.AddAsyncObjectPool(new MyComplexPolicy());
```

---

## ⚙️ Configuration Modes

You can control the pool's behavior via `ObjectPoolOptions.AccessMode`:

| Mode | Description | Best For |
| :--- | :--- | :--- |
| **FIFO (Default)** | **Lock-Free / Elastic.** If the pool is empty, it immediately creates a new object. It never blocks the thread. | CPU-bound objects (`StringBuilder`, buffers) where latency matters most. |
| **Bounded** | **Throttled / Blocking.** If the pool reaches `MaximumRetained` limit, `GetAsync()` will **await** until an object is returned. | Limited resources (DB Connections, Throttled API Clients) to prevent system overload. |

---

## 🛡️ Developer Experience: Safety Nets

These features are **active only in DEBUG builds** and have **zero performance impact** in Release.

### 1. Leak Detection
If you forget to dispose a `PooledObject`, the library will catch it during Garbage Collection and log a fatal error with the stack trace.

**Code with Bug:**
```csharp
var lease = pool.Lease(); // forgot 'using' or 'Dispose()'
// ... variable goes out of scope ...
```

**Output Window:**
```text
[WIAOJ.OBJECTPOOLING LEAK DETECTED]
Object 'StringBuilder' leaked! It was collected without Dispose().
Origin:
    at MyApp.Services.MyService.Method() in C:\Projects\MyApp\Service.cs:line 42
```

### 2. Return Validation
Ensure "dirty" objects don't pollute your pool.

```csharp
builder.Services.AddObjectPool<List<string>>(options => 
{
#if DEBUG
    options.OnReturnValidation = obj => 
    {
        var list = (List<string>)obj;
        if (list.Count > 0) throw new InvalidOperationException("List was not cleared!");
    };
#endif
});
```

---

## 📄 License

This project is licensed under the MIT License.