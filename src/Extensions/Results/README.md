# Wiaoj.Results

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.Results.svg)](https://www.nuget.org/packages/Wiaoj.Results)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Wiaoj.Results** is a high-performance, zero-dependency library that implements the **Result Pattern** for .NET. It allows you to write robust, expressive, and type-safe code by replacing exceptions with a functional approach known as **Railway Oriented Programming (ROP)**.

Designed for Domain-Driven Design (DDD) and Clean Architecture, it helps you manage control flow elegantly without the performance overhead of exceptions.

## 🚀 Features

*   **Zero Dependencies:** Built with pure C#, no external NuGet packages.
*   **High Performance:** Uses lightweight `struct` types to minimize heap allocations.
*   **Rich Error Model:** Structured errors with `Code`, `Description`, `Type`, and `Metadata`.
*   **Fluent API:** Chain operations easily with `.Then()`, `.ThenAsync()`, and `.Match()`.
*   **Async First:** First-class support for `Task` based operations.
*   **Implicit Conversions:** Write clean code without verbose syntax.

## 📦 Installation

Install via NuGet Package Manager:

```bash
dotnet add package Wiaoj.Results
```

## ⚡ Quick Start

### 1. Returning Results

Instead of throwing exceptions or returning null, return a `Result<T>`.

```csharp
using Wiaoj.Results;

public class UserService 
{
    public Result<User> GetUser(int id) 
    {
        if (id <= 0) 
        {
            // Implicit conversion from Error to Result<User>
            return Error.Validation("User.InvalidId", "Id must be positive.");
        }

        var user = _repository.Find(id);
        
        if (user is null) 
        {
            // Standard error types: NotFound, Validation, Conflict, Unauthorized...
            return Error.NotFound("User.NotFound", $"User with id {id} not found.");
        }

        // Implicit conversion from Value to Result<User>
        return user;
    }
}
```

### 2. Handling Results (Pattern Matching)

Handle success and failure cases explicitly using `.Match()` or `.Switch()`.

```csharp
var result = service.GetUser(1);

string response = result.Match(
    user => $"User found: {user.Name}",
    errors => $"Failed: {errors[0].Description}"
);
```

### 3. Railway Oriented Programming (Chaining)

Avoid "Nested If Hell" by chaining operations. If any step fails, the chain stops and returns the error.

```csharp
public async Task<Result<Guid>> RegisterUserAsync(UserDto dto)
{
    // The chain flows naturally: Validation -> Creation -> Email -> Result
    return await ValidateUserAsync(dto)
        .ThenAsync(validDto => CreateUserInDbAsync(validDto))
        .ThenAsync(user => SendWelcomeEmailAsync(user))
        .ThenAsync(user => Result.Success(user.Id));
}
```

### 4. Void Operations

For methods that don't return a value (void), use `Result<Success>` (or simply return `Result.Success()`).

```csharp
public Result<Success> DeleteUser(int id)
{
    if (!_repo.Exists(id))
    {
        return Error.NotFound();
    }

    _repo.Delete(id);
    
    return Result.Success();
}
```

## 🌐 Web API Integration

Easily map results to HTTP responses in your Controllers or Minimal APIs.

```csharp
[HttpGet("{id}")]
public async Task<IResult> GetUser(int id)
{
    var result = await _service.GetUserAsync(id);

    return result.Match(
        user => Results.Ok(user),
        errors => Results.Problem(
            statusCode: GetStatusCode(errors[0].Type), 
            title: errors[0].Description
        )
    );
}

// Helper to map ErrorType to Status Code
private int GetStatusCode(ErrorType type) => type switch
{
    ErrorType.Validation   => StatusCodes.Status400BadRequest,
    ErrorType.NotFound     => StatusCodes.Status404NotFound,
    ErrorType.Conflict     => StatusCodes.Status409Conflict,
    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
    ErrorType.Forbidden    => StatusCodes.Status403Forbidden,
    _                      => StatusCodes.Status500InternalServerError
};
```

## 🛠 Error Types

The library comes with built-in "Smart Enum" error types to categorize issues:

| ErrorType      | HTTP Equivalent | Use Case |
| :---           | :--- | :--- |
| `Failure`      | 500 | General failures or external service errors. |
| `Unexpected`   | 500 | Unhandled exceptions or bugs. |
| `Validation`   | 400 | Invalid user input. |
| `NotFound`     | 404 | Resource does not exist. |
| `Conflict`     | 409 | Duplicate resource or logic conflict. |
| `Unauthorized` | 401 | Authentication failed. |
| `Forbidden`    | 403 | Authenticated but not allowed. |

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.