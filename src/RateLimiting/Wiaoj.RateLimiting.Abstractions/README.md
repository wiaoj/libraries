# Wiaoj.RateLimiting.Abstractions

Core contracts, interfaces, and value primitives for the `Wiaoj.RateLimiting` library.

This package defines the public API surface, decision primitives, and builder contracts required to consume, implement, or extend rate limiting components without referencing concrete algorithm implementations, distributed storage engines, or ASP.NET Core transport layers.

---

## Installation

```bash
dotnet add package Wiaoj.RateLimiting.Abstractions
```

---

## Core Types and Contracts

### 1. `RateLimitDecision` (`readonly record struct`)

Represents the immutable result of a rate limit check:

- `IsAllowed` (`bool`): Indicates whether the request is allowed to proceed.
- `RetryAfter` (`TimeSpan?`): The mandatory backoff duration before retrying if the request was denied.
- `Remaining` (`long?`): The remaining capacity or token units for the key.

#### Static Factory Methods:
- `RateLimitDecision.Allowed()`: Permitted with unknown remaining quota.
- `RateLimitDecision.Allowed(long remaining)`: Permitted with explicit remaining units. Throws `ArgumentOutOfRangeException` if `remaining < 0`.
- `RateLimitDecision.Denied(TimeSpan retryAfter)`: Denied with retry duration, sets remaining to 0. Throws `ArgumentOutOfRangeException` if `retryAfter < TimeSpan.Zero`.
- `RateLimitDecision.Denied(TimeSpan retryAfter, long remaining)`: Denied with retry duration and remaining units. Throws `ArgumentOutOfRangeException` if `retryAfter < TimeSpan.Zero` or `remaining < 0`.

---

### 2. `IRateLimitAlgorithm`

The fundamental contract implemented by all rate limiting algorithms and decorators:

```csharp
public interface IRateLimitAlgorithm {
    ValueTask<RateLimitDecision> TryAcquireAsync(
        string key, 
        int cost, 
        CancellationToken cancellationToken = default);
}
```

Convenience overloads (e.g. `TryAcquireAsync(key)` with default cost 1) are provided via `RateLimitingExtensions`.

---

### 3. `IRateLimiter`

The primary service contract used by applications, middleware, and services to evaluate rate limit rules against named or default policies:

```csharp
public interface IRateLimiter {
    ValueTask<RateLimitDecision> TryAcquireAsync(
        string policyName, 
        string key, 
        int cost, 
        CancellationToken cancellationToken = default);

    ValueTask<RateLimitDecision> TryAcquireAsync(
        string key, 
        int cost, 
        CancellationToken cancellationToken = default);

    IRateLimitAlgorithm GetPolicy(string policyName);
}
```

---

### 4. `IRateLimiter<TPolicy>`

A strongly-typed, marker-based wrapper for Dependency Injection:

```csharp
public interface IRateLimiter<TPolicy> where TPolicy : notnull {
    ValueTask<RateLimitDecision> TryAcquireAsync(
        string key, 
        int cost, 
        CancellationToken cancellationToken = default);
}
```

Calls are automatically routed to the policy matching `typeof(TPolicy).Name`.

---

### 5. `IRateLimitPolicyBuilder`

A minimal builder contract for configuring an individual rate limit policy within the dependency injection container:

```csharp
public interface IRateLimitPolicyBuilder {
    IServiceCollection Services { get; }
    string PolicyName { get; }
    IRateLimitPolicyBuilder UseAlgorithm(Func<IServiceProvider, IRateLimitAlgorithm> factory);
    IRateLimitPolicyBuilder AddDecorator(Func<IServiceProvider, IRateLimitAlgorithm, IRateLimitAlgorithm> decorator);
}
```

---

## Usage Examples

### Consuming `IRateLimiter` with a Named Policy

```csharp
using Wiaoj.RateLimiting;

public sealed class PaymentService(IRateLimiter rateLimiter) {

    public async Task<bool> ProcessPaymentAsync(string customerId, CancellationToken cancellationToken) {
        RateLimitDecision decision = await rateLimiter.TryAcquireAsync(
            "payments", 
            customerId, 
            cost: 1, 
            cancellationToken: cancellationToken);

        if (!decision.IsAllowed) {
            TimeSpan? waitDuration = decision.RetryAfter;
            return false;
        }

        return true;
    }
}
```

---

### Consuming `IRateLimiter<TPolicy>` with a Strongly-Typed Tag

```csharp
using Wiaoj.RateLimiting;

public sealed class LoginController(IRateLimiter<AuthRateLimitPolicy> rateLimiter) {

    public async Task<bool> AuthenticateAsync(string clientIp, CancellationToken cancellationToken) {
        RateLimitDecision decision = await rateLimiter.TryAcquireAsync(
            clientIp, 
            cost: 1, 
            cancellationToken: cancellationToken);

        return decision.IsAllowed;
    }
}

public sealed class AuthRateLimitPolicy;
```

---

## License

This project is licensed under the MIT License.