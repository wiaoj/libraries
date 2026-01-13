# Wiaoj.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.Abstractions.svg)](https://www.nuget.org/packages/Wiaoj.Abstractions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Wiaoj.Abstractions** is a comprehensive library providing essential, generic interfaces and extension methods to standardize common architectural patterns in .NET applications.

It eliminates ambiguity in standard .NET interfaces (like `ICloneable`), provides robust contracts for Object Construction (Builders/Factories), and simplifies State Transfer between objects.

## 🚀 Key Features

*   **Type-Safe Cloning:** Replaces the ambiguous `object ICloneable.Clone()` with strongly-typed `IDeepCloneable<T>` and `IShallowCloneable<T>`.
*   **State Transfer:** Interfaces (`ICopyFrom`, `ICopyTo`) and extensions (`CopyIntoNew`) for transferring state between instances efficiently.
*   **Async Factories:** Standardized `IAsyncFactory` interfaces to handle complex object creation requiring asynchronous initialization (e.g., DB connections).
*   **Builder Pattern:** Abstracted `IBuilder<T>` and `IAsyncBuilder<T>` contracts to standardize object construction logic.
*   **Rich Extensions:** Zero-boilerplate extension methods to simplify usage and reduce code duplication.

## 📦 Installation

Install via NuGet Package Manager:

```bash
dotnet add package Wiaoj.Abstractions
```

> **Note:** This library depends on `Wiaoj.Preconditions` for argument validation.

---

## ⚡ Usage Examples

### 1. Robust Cloning (Deep vs. Shallow)
Standard .NET `ICloneable` is ambiguous. This library forces you to be explicit about whether a clone is a "Deep Copy" (independent instance) or a "Shallow Copy" (shared references).

```csharp
using Wiaoj.Abstractions;

// Implement specific strategies
public class UserProfile : IDeepCloneable<UserProfile>, IShallowCloneable<UserProfile>
{
    public string Username { get; set; }
    public Address Address { get; set; } // Reference type

    // 1. Deep Clone: Create a totally new instance with new references
    public UserProfile DeepClone()
    {
        return new UserProfile 
        { 
            Username = this.Username, 
            Address = this.Address.DeepClone() // Assuming Address also implements IDeepCloneable
        };
    }

    // 2. Shallow Clone: Copy values, share references
    public UserProfile ShallowClone()
    {
        return new UserProfile 
        { 
            Username = this.Username, 
            Address = this.Address 
        };
    }
}

// --- Usage ---
var original = new UserProfile { ... };

// Specific usage
var deepCopy = original.DeepClone();
var shallowCopy = original.ShallowClone();

// Polymorphic usage via Extensions (defaults to DeepClone)
var clone = original.Clone(); 
var specificClone = original.Clone(CloneKind.Shallow);
```

### 2. State Copying & Prototyping
Instead of mapping properties manually or using heavy reflection libraries, implement `ICopyFrom` to define how state is transferred.

```csharp
public class AppSettings : ICopyFrom<AppSettings>
{
    public int Timeout { get; set; }
    public string Theme { get; set; }

    public void CopyFrom(AppSettings source)
    {
        this.Timeout = source.Timeout;
        this.Theme = source.Theme;
    }
}

// --- Usage ---
var defaultSettings = new AppSettings { Timeout = 30, Theme = "Dark" };

// Scenario A: Update an existing instance
var currentSettings = new AppSettings();
currentSettings.CopyFrom(defaultSettings);

// Scenario B: Create a fresh copy (Prototype Pattern)
// Uses the default constructor (new())
var newSettings = defaultSettings.CopyIntoNew(); 
```

### 3. Advanced Copying (Dependency Injection Friendly)
The `CopyIntoNew` extension is powerful when the target object cannot be created with a simple `new()` (e.g., needs dependencies).

```csharp
// Scenario: Creating a copy using a Factory or DI Container
var prototype = new ServiceConfig { Retries = 3 };

// Pass a factory function to create the new instance, then copy state into it
var newInstance = prototype.CopyIntoNew(() => 
{
    // Example: Resolve from DI
    return _serviceProvider.GetRequiredService<ServiceConfig>(); 
});

// Pass arguments to the factory
var customizedInstance = prototype.CopyIntoNew(
    factory: (name) => new ServiceConfig(name), 
    arg: "CustomService"
);
```

### 4. Asynchronous Factories
Constructors in C# cannot be `async`. Use `IAsyncFactory` for objects that need awaitable initialization logic (Database connections, File handles, Network streams).

```csharp
// Factory that creates an initialized Redis connection
public class RedisConnectionFactory : IAsyncFactory<IConnectionMultiplexer, string>
{
    public async Task<IConnectionMultiplexer> CreateAsync(string connectionString, CancellationToken ct = default)
    {
        var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        return connection;
    }
}

// --- Usage ---
var factory = new RedisConnectionFactory();
var redis = await factory.CreateAsync("localhost:6379");
```
*Note: Supports generic factories with up to 3 arguments.*

### 5. Builder Pattern
Standardize how you build complex objects, both synchronously and asynchronously.

```csharp
public class ReportBuilder : IAsyncBuilder<Report>
{
    public async Task<Report> BuildAsync(CancellationToken ct = default)
    {
        var data = await _repository.GetDataAsync(ct);
        return new Report(data);
    }
}

// --- Usage ---
IAsyncBuilder<Report> builder = new ReportBuilder();
var report = await builder.BuildAsync();
```

---

## 🛠 API Reference

### Cloning Interfaces
| Interface | Description |
| :--- | :--- |
| **`IDeepCloneable<T>`** | Contract for creating a full, independent copy of an object graph. |
| **`IShallowCloneable<T>`** | Contract for creating a lightweight copy (shared references). |
| **`ICloneable<T>`** | Aggregates both Deep and Shallow interfaces. Enables `obj.Clone(CloneKind.Deep)` usage. |

### State Interfaces
| Interface | Description |
| :--- | :--- |
| **`ICopyFrom<T>`** | Defines how an object pulls state *from* a source. |
| **`ICopyTo<T>`** | Defines how an object pushes state *to* a target. |

### Construction Interfaces
| Interface | Description |
| :--- | :--- |
| **`IBuilder<T>`** | Contract for synchronous object construction. |
| **`IAsyncBuilder<T>`** | Contract for asynchronous object construction. |
| **`IAsyncFactory<T>`** | Contract for async object creation (0 args). |
| **`IAsyncFactory<T, T1...>`** | Contract for async object creation with arguments (supports up to 3 args). |

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.