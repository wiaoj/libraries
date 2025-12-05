# Wiaoj.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.Abstractions.svg)](https://www.nuget.org/packages/Wiaoj.Abstractions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Wiaoj.Abstractions** provides a set of essential, generic interfaces and extension methods to standardize common architectural patterns in .NET applications.

It focuses on type safety and clarity, solving common ambiguities found in standard .NET interfaces (such as the confusion between Deep vs. Shallow cloning).

## 🚀 Features

*   **Explicit Cloning:** Separated `IDeepCloneable<T>` and `IShallowCloneable<T>` interfaces to eliminate ambiguity.
*   **State Copying:** `ICopyFrom<T>` and `ICopyTo<T>` interfaces for transferring state between instances without creating new objects.
*   **Async Factories:** Standardized `IAsyncFactory<T>` interfaces for handling asynchronous object creation and initialization.
*   **Zero-Boilerplate Extensions:** Rich set of extension methods like `DeepClone()`, `CopyIntoNew()`, and factory integrations.

## 📦 Installation

Install via NuGet Package Manager:

```bash
dotnet add package Wiaoj.Abstractions
```

## ⚡ Usage Examples

### 1. Robust Cloning
Resolve the ambiguity of .NET's `ICloneable` by explicitly defining cloning behavior.

```csharp
using Wiaoj.Abstractions;

public class User : IDeepCloneable<User>, IShallowCloneable<User>
{
    public string Name { get; set; }
    public Address Address { get; set; } // Reference type

    public User DeepClone()
    {
        return new User 
        { 
            Name = this.Name, 
            Address = this.Address.DeepClone() // Recursively clone
        };
    }

    public User ShallowClone()
    {
        return new User 
        { 
            Name = this.Name, 
            Address = this.Address // Copy reference
        };
    }
}

// Usage
var user = new User { Name = "John", Address = new Address("NY") };
var deepCopy = user.DeepClone();       // Completely independent copy
var shallowCopy = user.ShallowClone(); // Shares the Address reference
```

### 2. Copying State (Copy Pattern)
Useful for updating existing instances or implementing "Prototype" patterns where you want to copy state into a fresh instance created by a specific factory.

```csharp
public class Configuration : ICopyFrom<Configuration>
{
    public int Timeout { get; set; }

    public void CopyFrom(Configuration source)
    {
        this.Timeout = source.Timeout;
    }
}

// Usage
var defaultConfig = new Configuration { Timeout = 30 };

// 1. Copy into a new instance using default constructor
var config1 = defaultConfig.CopyIntoNew();

// 2. Copy into a new instance using a custom factory (e.g., DI container)
var config2 = defaultConfig.CopyIntoNew(() => _serviceProvider.GetRequiredService<Configuration>());
```

### 3. Asynchronous Factories
Ideal for creating objects that require asynchronous initialization (e.g., establishing a database connection or loading remote configuration).

```csharp
public class ConnectionFactory : IAsyncFactory<DatabaseConnection, string>
{
    public async Task<DatabaseConnection> CreateAsync(string connectionString, CancellationToken ct = default)
    {
        var connection = new DatabaseConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}

// Usage
var factory = new ConnectionFactory();
var db = await factory.CreateAsync("Server=...;");
```

## 🛠 Interfaces Overview

| Interface | Description |
| :--- | :--- |
| **`IDeepCloneable<T>`** | Defines a contract for creating a deep copy of an object (cloning all nested references). |
| **`IShallowCloneable<T>`** | Defines a contract for creating a shallow copy (copying values and references only). |
| **`ICloneable<T>`** | Combines both Deep and Shallow strategies, allowing runtime selection via `CloneKind`. |
| **`ICopyFrom<T>`** | Allows an object to populate its own state from a source object. |
| **`ICopyTo<T>`** | Allows an object to push its state into a target object. |
| **`IAsyncFactory<T>`** | Standardizes async object creation (supports up to 3 arguments). |

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.