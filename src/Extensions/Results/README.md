# Wiaoj.Results

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.Results.svg)](https://www.nuget.org/packages/Wiaoj.Results)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Wiaoj.Results** is a high-performance, zero-dependency library that implements the **Result Pattern** for .NET. It allows you to write robust, expressive, and type-safe code by replacing exception-based control flow with a functional approach known as **Railway Oriented Programming (ROP)**.

Designed with Domain-Driven Design (DDD) and Clean Architecture in mind, it helps you manage application flow elegantly while minimizing performance overhead.

## 🚀 Features

*   **Zero Dependencies:** Built with pure C#, no external packages required.
*   **High Performance:** Uses lightweight `readonly record struct` types to minimize heap allocations and garbage collection pressure.
*   **Rich Error Model:** Structured, immutable errors featuring `Code`, `Description`, `Type`, and extensible `Metadata`.
*   **Advanced Async Support:** First-class extension methods for both `Task` and `ValueTask` to ensure maximum performance in asynchronous pipelines.
*   **Elegant Control Flow:** Eliminate "Nested If Hell" using fluent combinators like `.Then()`, `.Map()`, `.Ensure()`, `.Do()`, and `.Match()`.
*   **Collection Power:** Easily combine, filter, or aggregate `IEnumerable<Result<T>>` collections.
*   **Safe Resource Management:** Safely handle `IDisposable` and `IAsyncDisposable` payloads using `.Consume()` and `.ConsumeAsync()`.
*   **Exception Safety:** Safely wrap throwing code and 3rd-party libraries using `Result.Try` and `Result.TryAsync`.

---

## 📦 Installation

Install via the .NET CLI:

```bash
dotnet add package Wiaoj.Results
```

Or via the Package Manager Console:

```powershell
Install-Package Wiaoj.Results
```

---

## ⚡ Core Concepts & Quick Start

### 1. Returning Results (Success or Failure)

Instead of throwing exceptions or returning `null`, return a `Result<T>`. The library supports **implicit conversions** from both raw values and `Error` objects, keeping your code clean.

```csharp
using Wiaoj.Results;

public class UserService 
{
    public Result<User> GetUser(int id) 
    {
        if (id <= 0) 
            return Error.Validation("User.InvalidId", "ID must be positive.");

        var user = _repository.Find(id);
        
        if (user is null) 
            return Error.NotFound("User.NotFound", $"User with id {id} was not found.");

        // Implicitly converts the User object to a successful Result<User>
        return user; 
    }
}
```

### 2. Void Operations (`Result<Success>`)

For operations that do not return a specific value, use `Result<Success>` (or simply return `Result.Success()`). `Success` is a zero-allocation, 1-byte struct optimized for high performance.

```csharp
public Result<Success> DeleteUser(int id)
{
    if (!_repo.Exists(id))
        return Error.NotFound();

    _repo.Delete(id);
    return Result.Success();
}
```

### 3. Handling Results (Pattern Matching)

Extract values or handle errors gracefully at the edges of your application (e.g., UI or API controllers) using `.Match()` or `.Switch()`.

```csharp
var result = service.GetUser(1);

// Match: Returns a value based on the outcome
string response = result.Match(
    user => $"Welcome back, {user.Name}!",
    errors => $"Failed: {errors[0].Description}"
);

// Switch: Executes an action based on the outcome (returns void)
result.Switch(
    user => Console.WriteLine($"Success: {user.Email}"),
    errors => Console.WriteLine($"Error Code: {errors[0].Code}")
);
```

---

## 🚂 Railway Oriented Programming (Chaining)

Instead of writing nested `if(!result.IsSuccess)` checks, chain your operations. If any step fails, the pipeline short-circuits and bypasses subsequent steps, propagating the error down the chain.

```csharp
public async Task<Result<Guid>> RegisterUserAsync(UserDto dto)
{
    return await ValidateDtoAsync(dto)                                   // 1. Returns Result<UserDto>
        .EnsureAsync(IsEmailUniqueAsync, Error.Conflict("Email.InUse"))  // 2. Fails if email exists
        .ThenAsync(validDto => CreateUserInDbAsync(validDto))            // 3. Executes next step returning Result<User>
        .DoAsync(user => SendWelcomeEmailAsync(user))                    // 4. Side-effect: runs only on success
        .MapAsync(user => user.Id);                                      // 5. Transforms Result<User> to Result<Guid>
}
```

---

## 🛡️ Rich Error Model

The library uses a "Smart Enum" approach (`ErrorType`) to categorize errors, making it trivial to map them to HTTP status codes or log severity levels.

### Built-in Error Types

| Factory Method | Suggested HTTP Code | Use Case |
| :--- | :--- | :--- |
| `Error.Failure()` | 500 | General failures or default unhandled states. |
| `Error.Validation()` | 400 | Invalid input formats or schema violations. |
| `Error.NotFound()` | 404 | The requested resource does not exist. |
| `Error.Conflict()` | 409 | Duplicate resource or business logic conflict. |
| `Error.Unauthorized()` | 401 | Authentication is required but missing/invalid. |
| `Error.Forbidden()` | 403 | Authenticated, but lacks required permissions. |
| `Error.UnprocessableEntity()` | 422 | Syntactically valid request, but semantic rule violation. |
| `Error.RateLimitExceeded()` | 429 | Caller has sent too many requests. |
| `Error.ServiceUnavailable()`| 503 | Downstream dependency is temporarily unreachable. |
| `Error.Timeout()` | 408 / 504 | Operation did not complete within the allowed time. |

### Custom Errors & Metadata

You can define domain-specific error types and attach contextual metadata to your errors.

```csharp
public static class AppErrorTypes
{
    public static readonly ErrorType Maintenance = new("Maintenance");
}

// Emitting a custom error with metadata
return Error.Custom(AppErrorTypes.Maintenance, "System.Offline", "System is under maintenance.")
            .WithMetadata("RetryAfter", DateTime.UtcNow.AddHours(1))
            .WithMetadata("TicketId", 12345);
```

### Multiple Errors

Useful for returning aggregated validation errors instead of failing at the very first bad input.

```csharp
List<Error> errors =[
    Error.Validation("Name.Required", "Name cannot be empty."),
    Error.Validation("Age.Min", "Age must be at least 18.")
];

return Error.Multiple(errors); // Returns Result<Success> containing all errors
```

---

## 🛠️ Advanced Features

### Safely Wrapping Exceptions (`Try` / `TryAsync`)
Easily convert exceptions from 3rd-party libraries (or the BCL) into `Result` objects.

```csharp
// Automatically maps TimeoutException, UnauthorizedAccessException, etc. to correct ErrorTypes
Result<string> content = await Result.TryAsync(ct => File.ReadAllTextAsync("data.txt", ct));

// Or provide a custom exception mapper:
Result<int> parsed = Result.Try(
    () => int.Parse("bad_input"),
    ex => Error.Validation("ParseError", ex.Message)
);
```

### Nullability Bridges (`ToResult` / `EnsureNotNull`)
Convert `null` reference returns from existing APIs (like Entity Framework) into strict `Result` types.

```csharp
var user = await dbContext.Users.FindAsync(id);

// If user is null, returns the provided NotFound error.
return user.ToResult(Error.NotFound("User.NotFound", "User not found."));

// Or chaining on an existing Result<T?>:
Result<User> strictResult = nullableResult.EnsureNotNull(Error.NotFound());
```

### Collection Combinators (`Combine`)
Process a batch of results. If all succeed, you get a list of values. If any fail, you get an aggregated list of *all* errors.

```csharp
IEnumerable<Result<User>> userResults = userIds.Select(id => GetUser(id));

// Returns Result<IReadOnlyList<User>>. 
// If *any* GetUser call failed, `combined` becomes a failure holding all collected errors.
Result<IReadOnlyList<User>> combined = userResults.Combine();
```

### Resource Management (`Consume` / `DisposeValue`)
When your `Result<T>` holds an `IDisposable` or `IAsyncDisposable` resource (e.g., a `Stream` or `HttpResponseMessage`), you can ensure it gets disposed right after execution.

```csharp
await Result.TryAsync(ct => httpClient.GetAsync(url, ct))
    .ConsumeAsync(async (response, ct) => 
    {
        var data = await response.Content.ReadAsStringAsync(ct);
        Console.WriteLine(data);
        // The response is automatically disposed at the end of this block.
    });
```

### ValueTask Optimization
For high-performance scenarios (like cache lookups that are usually synchronous), the library provides specialized `.ThenAsync`, `.MapAsync`, and `.MatchAsync` overloads for `ValueTask<Result<T>>` to prevent unnecessary heap allocations.

---

## 🌐 Web API / Minimal APIs Integration

Mapping a `Result<T>` to an HTTP Response is straightforward using `.Match()`. Here is a common pattern for ASP.NET Core Minimal APIs or Controllers:

```csharp[HttpGet("{id}")]
public async Task<IResult> GetUser(int id)
{
    var result = await _userService.GetUserAsync(id);

    return result.Match(
        user => Results.Ok(user),
        errors => Results.Problem(
            statusCode: GetStatusCode(errors[0].Type), 
            title: errors[0].Description,
            extensions: errors[0].Metadata?.ToDictionary(k => k.Key, v => v.Value)
        )
    );
}

// Helper to map your ErrorTypes to standard HTTP Status Codes
private static int GetStatusCode(ErrorType type) => type.Name switch
{
    nameof(ErrorType.Validation)   => StatusCodes.Status400BadRequest,
    nameof(ErrorType.NotFound)     => StatusCodes.Status404NotFound,
    nameof(ErrorType.Conflict)     => StatusCodes.Status409Conflict,
    nameof(ErrorType.Unauthorized) => StatusCodes.Status401Unauthorized,
    nameof(ErrorType.Forbidden)    => StatusCodes.Status403Forbidden,
    nameof(ErrorType.RateLimit)    => StatusCodes.Status429TooManyRequests,
    nameof(ErrorType.Unavailable)  => StatusCodes.Status503ServiceUnavailable,
    _                              => StatusCodes.Status500InternalServerError
};
```

---

## 🤝 Contributing

Contributions, bug reports, and feature requests are welcome! Feel free to open an issue or submit a Pull Request on the GitHub repository.

## 📄 License

This project is licensed under the **MIT License**.