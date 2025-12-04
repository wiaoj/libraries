# Wiaoj Dependency Injection Extensions

A lightweight, high-performance extension library for **Microsoft.Extensions.DependencyInjection**. This package provides a robust implementation of the **Decorator Pattern**, allowing you to wrap registered services with cross-cutting concerns (like logging, caching, or resilience policies) without altering their underlying registration logic.

Built using modern C# features, it ensures type safety, preserves service lifetimes, and handles complex dependency chains effortlessly.

## 🌟 Features

*   **Decorator Pattern Made Easy:** Apply decorators to services with a single line of code.
*   **Lifetime Preservation:** Automatically respects the lifetime (`Singleton`, `Scoped`, or `Transient`) of the original service.
*   **Universal Compatibility:** Works with all service registration types:
    *   Implementation Type (`AddSingleton<ISvc, Svc>`)
    *   Factory Delegates (`AddScoped(sp => new Svc())`)
    *   Instances (`AddSingleton(new Svc())`)
*   **Circular Dependency Safety:** Uses `ActivatorUtilities` to instantiate original services, preventing `StackOverflowException` during resolution.
*   **Modern Syntax:** Leverages C# "Explicit Extension" syntax for cleaner API surface.

## 📦 Installation

```bash
dotnet add package Wiaoj.Extensions.DependencyInjection
```

## 🚀 Usage

### 1. Type-Based Decoration
The most common scenario is wrapping an interface with a decorator class that accepts the inner service in its constructor.

**Scenario:** You have an `ICalculator` and want to add logging without changing the logic.

```csharp
// 1. Define your service
public interface ICalculator { int Add(int a, int b); }
public class Calculator : ICalculator { ... }

// 2. Define your decorator
public class LoggingCalculatorDecorator : ICalculator {
    private readonly ICalculator _inner;
    private readonly ILogger<LoggingCalculatorDecorator> _logger;

    // The inner service is injected automatically
    public LoggingCalculatorDecorator(ICalculator inner, ILogger<LoggingCalculatorDecorator> logger) {
        _inner = inner;
        _logger = logger;
    }

    public int Add(int a, int b) {
        _logger.LogInformation($"Adding {a} and {b}");
        return _inner.Add(a, b);
    }
}

// 3. Register and Decorate
var services = new ServiceCollection();

// Register the base service
services.AddSingleton<ICalculator, Calculator>();

// Apply the decorator using Wiaoj extensions
services.Decorate<ICalculator, LoggingCalculatorDecorator>();
```

### 2. Factory-Based Decoration
For more complex scenarios where manual construction is required (e.g., passing specific parameters or resolving dependencies manually).

```csharp
services.AddScoped<IUserService, UserService>();

services.Decorate<IUserService>((innerService, provider) => {
    var cache = provider.GetRequiredService<IMemoryCache>();
    // Manually create the decorator
    return new CachingUserServiceDecorator(innerService, cache, expiration: TimeSpan.FromMinutes(10));
});
```

## 🔧 How It Works (Technical Details)

When you call `Decorate<TService, TDecorator>()`, the library performs the following steps:

1.  **Descriptor Lookup:** It finds the last registered `ServiceDescriptor` for `TService`.
2.  **Factory Replacement:** It replaces the original registration with a new implementation factory.
3.  **Instance Resolution:** Inside the new factory:
    *   It resolves or creates the **Original Service** instance. Critically, if the original service was registered via type (not factory), it creates the instance using `ActivatorUtilities.CreateInstance` instead of resolving from the container recursively. This avoids the infinite loop trap common in naive decorator implementations.
4.  **Decorator Composition:** It instantiates the **Decorator**, injecting the original service instance into it.
5.  **Lifetime Guard:** The new registration inherits the exact same `ServiceLifetime` as the original one.
 
## 🤝 Contributing

Contributions are welcome! Please ensure that any PRs maintain the existing code style and include unit tests covering the new functionality.

## 📄 License

Licensed under the MIT License.