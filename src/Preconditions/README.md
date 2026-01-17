# Wiaoj.Preconditions (Preca)

A high-performance, modern, and zero-allocation focused precondition and guard clause library for .NET.

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.Preconditions.svg)](https://www.nuget.org/packages/Wiaoj.Preconditions/)
![License](https://img.shields.io/github/license/wiaoj/Preca)
![Dotnet Version](https://img.shields.io/badge/dotnet-10.0-blue)

## 🚀 Why Preca?

- **Extreme Performance:** Built with `[MethodImpl(MethodImplOptions.AggressiveInlining)]` and the `Thrower` pattern to keep the "hot path" clean for the CPU cache.
- **Clean Stack Traces:** Uses `[StackTraceHidden]` so that exceptions point directly to the caller, not the library internals.
- **Modern .NET Core:** Native support for C# 12+ and .NET 8+ features like **Generic Math** (`ISignedNumber`, `IComparisonOperators`) and **Buffers** (`ReadOnlySpan<T>`, `Memory<T>`).
- **Zero Allocation:** Designed to avoid unnecessary boxing or allocations during successful validation.
- **Fluent & Static:** Offers both a standard static API (`Preca.ThrowIfNull`) and a fluent gateway via extensions.

## 📦 Installation

Install via NuGet:

```bash
dotnet add package Wiaoj.Preconditions
```

## 🛠 Usage

### Basic Validations

Standard null and string checks with automatic parameter name capturing:

```csharp
public void UpdateUser(string id, string displayName)
{
    // Throws PrecaArgumentNullException if null
    Preca.ThrowIfNull(id);
    
    // Throws PrecaArgumentException if null, empty, or whitespace
    Preca.ThrowIfNullOrWhiteSpace(displayName);
}
```

### Numeric & Range Validations (Generic Math)

Preca leverages .NET Generic Math to provide type-safe validations for any numeric type (`int`, `decimal`, `double`, `BigInteger`, etc.):

```csharp
public void ProcessOrder(int quantity, decimal price)
{
    Preca.ThrowIfNegativeOrZero(quantity);
    Preca.ThrowIfLessThan(price, 0.99m);
    
    // Inclusive range check
    Preca.ThrowIfOutOfRange(quantity, 1, 1000);
}
```

### Fluent API Extensions

Use the `Extensions` gateway for a more fluid syntax or when you want to return the validated value:

```csharp
public class UserService
{
    private readonly string _apiKey;

    public UserService(string apiKey)
    {
        // Validates and returns the value in one line
        _apiKey = Preca.Extensions.ThrowIfNullOrWhiteSpace(apiKey);
    }
}
```

### Custom Exception Factories

When you need to throw domain-specific exceptions instead of standard ArgumentExceptions:

```csharp
Preca.ThrowIf(balance < total, () => new InsufficientFundsException("Account balance too low."));

// Or with state to avoid closure allocations
Preca.ThrowIfNull(user, (u) => new UserNotFoundException(u.Id), user);
```

### Buffer & Span Support

Optimized checks for memory-efficient types:

```csharp
public void ParseData(ReadOnlySpan<char> buffer)
{
    Preca.ThrowIfEmpty(buffer);
    Preca.ThrowIfEmptyOrWhiteSpace(buffer);
}
```

## 📋 Supported Validations

| Category | Methods |
| :--- | :--- |
| **Nullability** | `ThrowIfNull` |
| **Strings** | `ThrowIfNullOrEmpty`, `ThrowIfNullOrWhiteSpace` |
| **Numerics** | `ThrowIfNegative`, `ThrowIfPositive`, `ThrowIfZero`, `ThrowIfLessThan`, `ThrowIfGreaterThan`, `ThrowIfOutOfRange`, `ThrowIfMaxValue`, `ThrowIfMinValue` |
| **Floating Point** | `ThrowIfNaN`, `ThrowIfInfinity`, `ThrowIfSubnormal` |
| **Buffers** | `ThrowIfEmpty` (Span, ReadOnlySpan, Memory, ArraySegment) |
| **Value Types** | `ThrowIfDefault`, `ThrowIfEmpty` (Guid), `ThrowIfUnspecifiedKind` (DateTime), `ThrowIfUndefined` (Enum) |
| **Booleans** | `ThrowIf`, `ThrowIfTrue`, `ThrowIfFalse` |

## 🏗 Performance Design

Preca uses the **Thrower Pattern**. Static methods that throw exceptions are often not inlined by the JIT compiler because the code size is too large. 

Preca moves the `throw` statement to a specialized `Thrower` class. This allows the guard clause itself to be inlined into your method, effectively reducing the cost of a successful check to a single CPU branch instruction.