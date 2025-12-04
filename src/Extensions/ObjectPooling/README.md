# Wiaoj.ObjectPool

A high-performance, lightweight, and modern object pooling library for .NET, built on top of `Microsoft.Extensions.ObjectPool`.

`Wiaoj.ObjectPool` enhances the default implementation with a robust abstraction layer (`IObjectPool<T>`, `IPoolPolicy<T>`), fluent Dependency Injection integration, and a powerful **Developer Safety Net** to catch common pooling errors during development.

## Key Features

-   **🚀 High Performance:** Uses a `readonly struct` (`PooledObject<T>`) to ensure zero heap allocations for leasing and returning objects.
-   **🔌 Fluent Dependency Injection:** Simple and discoverable extension methods for easy setup in any `IServiceCollection`.
-   **💡 Lambda-based Policies:** Register pools for simple objects in a single line of code without needing to create a separate policy class.
-   **🔐 Clean Abstraction Layer:** Provides its own clean interfaces (`IObjectPool<T>`, `IPoolPolicy<T>`) to completely decouple your code from the underlying `Microsoft.Extensions.ObjectPool` implementation details. This makes your code more testable, flexible, and future-proof.
-   **🛡️ Developer Safety Net (Debug-Only):**
    -   **Leak Detection:** Automatically detects and reports when a pooled object is garbage-collected without being returned to the pool, pinpointing the exact line of code where it was leased.
    -   **Validation on Return:** Allows you to define a validation rule to ensure objects are returned to the pool in a clean state, catching bugs in your reset logic instantly.

## Installation

Install the package from NuGet:

```bash
dotnet add package Wiaoj.ObjectPool
```

## Getting Started: Basic Usage

The easiest way to use the library is with the lambda-based registration. In your `Program.cs` or startup configuration:

```csharp
using System.Text;
using Wiaoj.ObjectPool;

var builder = WebApplication.CreateBuilder(args);

// Register a pool for StringBuilder
builder.Services.TryAddPooledObject<StringBuilder>(
    // Factory: How to create a new StringBuilder
    factory: () => new StringBuilder(1024),
    
    // Resetter: How to clean up a StringBuilder for reuse
    // Should return 'true' on success.
    resetter: sb => 
    {
        sb.Clear();
        return true;
    },
    
    // Options: Configure the pool's behavior
    configureOptions: options => 
    {
        options.MaximumRetained = 50;
    }
);

// ... later in your application, inject IObjectPool<T>
public class MyService
{
    private readonly IObjectPool<StringBuilder> _stringBuilderPool;

    public MyService(IObjectPool<StringBuilder> stringBuilderPool)
    {
        _stringBuilderPool = stringBuilderPool;
    }

    public string ProcessData()
    {
        // Lease an object from the pool. The 'using' statement guarantees
        // it's returned to the pool, even if exceptions occur.
        using PooledObject<StringBuilder> pooledSb = _stringBuilderPool.Lease();
        
        // Get the actual StringBuilder instance
        StringBuilder sb = pooledSb.Item; 
        sb.AppendLine("Hello from the pool!");
        
        return sb.ToString();
    }
}
```

## Advanced Usage

### Using a Custom Policy Class

For more complex objects or to better organize your pooling logic, you can create a dedicated policy class. This approach works best when your pooled object implements the `IResettable` interface.

**1. Implement `IResettable` on Your Object:**

An object knowing how to reset itself is a clean design pattern.

```csharp
using Wiaoj.ObjectPool;

public class ReportContext : IResettable
{
    public MemoryStream ReportStream { get; } = new();

    // Method from the IResettable interface
    public bool TryReset()
    {
        ReportStream.Position = 0;
        ReportStream.SetLength(0);
        return true;
    }
}
```

**2. Create a Policy Class Implementing `IPoolPolicy<T>`:**

The policy defines how the pool should create and reset objects.

```csharp
using Wiaoj.ObjectPool;

public class ReportContextPolicy : IPoolPolicy<ReportContext>
{
    public ReportContext Create() => new();

    // The policy delegates the reset task to the object itself.
    public bool TryReset(ReportContext obj) => obj.TryReset();
}
```

**3. Register It with Dependency Injection:**

```csharp
builder.Services.TryAddPooledObject<ReportContext, ReportContextPolicy>();
```

## The Developer Safety Net (Debug-Only Diagnostics)

To prevent hard-to-find bugs during development, the library includes two powerful diagnostic tools that are **only active in `DEBUG` builds**, meaning they have **zero performance impact** on your release application.

### 1. Leak Detection

This feature detects when you forget to `Dispose` a `PooledObject<T>`.

**Problem Code:**

```csharp
// DON'T DO THIS! The 'using' statement is missing.
var pooledSb = _pool.Lease(); 
pooledSb.Item.Append("This object will be leaked...");
// When this method ends, Dispose() is never called.
```

**Resulting Output:**
After the Garbage Collector runs, you will see a detailed report in your Debug Output window:

```
-------------------------------------------------
[WIAOJ.OBJECTPOOLING LEAK DETECTED]
An object of type 'StringBuilder' was garbage-collected without being returned to the pool.
This is a memory leak and indicates that PooledObject<T>.Dispose() was not called.
Ensure the PooledObject<T> is used within a 'using' statement or its Dispose() method is called explicitly.

Leased at:
    at MyWebApp.MyServices.LeakyMethod(IObjectPool`1 pool) in C:\Projects\MyWebApp\MyServices.cs:line 42
    ...
-------------------------------------------------
```

This feature is enabled by default in `DEBUG` builds. You can disable it via `options.LeakDetectionEnabled = false;`.

### 2. Validation on Return

This feature catches bugs in your reset logic, ensuring you don't return "dirty" objects to the pool.

**Problem Code:**

```csharp
builder.Services.TryAddPooledObject<List<string>>(
    factory: () => new List<string>(),
    resetter: list => 
    {
        // BUG: The developer forgot to clear the list!
        return true;
    },
    configureOptions: options =>
    {
#if DEBUG
        // This validation rule will catch the bug above.
        options.OnReturnValidation = obj =>
        {
            var list = (List<string>)obj;
            if (list.Count > 0)
                throw new InvalidOperationException("Validation failed: List was not cleared!");
        };
#endif
    }
);
```

**Resulting Exception:**
When an object with data is returned to the pool, your application will immediately throw a descriptive exception, pointing you directly to the faulty reset logic.

```
System.InvalidOperationException: An object of type 'List`1' failed validation upon being returned to the pool. This indicates a bug in its reset logic (e.g., in `TryReset` or the lambda resetter). See inner exception for details.
 ---> System.InvalidOperationException: Validation failed: List was not cleared!
   --- End of inner exception stack trace ---
   at Wiaoj.ObjectPool.PooledObject`1.Dispose() in ...\PooledObject.cs:line 55
   ...```