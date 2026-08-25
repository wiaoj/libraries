# Wiaoj.DistributedCounter.Testing

Test doubles, controllable fake storage, time-travel utilities, and assertion helpers for the `Wiaoj.DistributedCounter` ecosystem.

This package allows developers to write fast, isolated unit and integration tests for counter-dependent application services without requiring a running Redis instance or external infrastructure.

---

## Installation

```bash
dotnet add package Wiaoj.DistributedCounter.Testing
```

---

## Key Features

- **`FakeCounterStorage`:** A thread-safe, in-memory test double for `ICounterStorage` with call counting, time-travel TTL expiration, flush history inspection, and failure simulation.
- **`DistributedCounterTestContext`:** An isolated, pre-wired test harness that automates dependency creation for `IDistributedCounterFactory` and `IDistributedCounterService`.
- **Fluent Assertions:** Custom assertion extensions (`ShouldHaveValue`, `ShouldHaveFlushed`, `ShouldHaveBatchFlushCount`) for verifying storage states.
- **Background Worker Synchronization:** `WaitForNextFlushAsync` helper to advance `FakeTimeProvider` and synchronize thread-pool timers without manual polling.
- **`WebApplicationFactory` Integration:** `UseFakeStorage()` builder extensions for integration testing in ASP.NET Core test hosts.

---

## Usage Examples

### 1. Unit Testing with `DistributedCounterTestContext`

Test services using a controlled fake time provider and isolated storage:

```csharp
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

public sealed class RateLimiterServiceTests {

    [Fact]
    public async Task WhenLimitExceeded_RejectsFurtherIncrements() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DistributedCounterTestContext context = new(timeProvider, options => {
            options.AddImmediateCounter<RateLimitTag>();
        });

        IDistributedCounterFactory factory = context.CreateFactory();
        IDistributedCounter<RateLimitTag> counter = factory.Create<RateLimitTag>();

        CancellationToken ct = CancellationToken.None;

        // Act: Consume quota (3 out of 3)
        await counter.IncrementAsync("user_101", 3, CounterExpiry.FromMinutes(1), ct);

        // Attempt 4th request
        CounterLimitResult result = await counter.TryIncrementAsync(
            "user_101", 
            limit: 3, 
            amount: 1, 
            expiry: CounterExpiry.FromMinutes(1), 
            cancellationToken: ct);

        // Assert
        Assert.False(result.IsAllowed);
        context.Storage.ShouldHaveValue(counter.ForKey("user_101").Key, 3);
    }
}

public sealed class RateLimitTag;
```

---

### 2. Time-Travel and Expiration Testing

Verify sliding expiration windows by advancing `FakeTimeProvider`:

```csharp
[Fact]
public async Task AfterWindowExpires_ResetsQuotaAutomatically() {
    // Arrange
    FakeTimeProvider timeProvider = new();
    DistributedCounterTestContext context = new(timeProvider, options => {
        options.AddImmediateCounter<RateLimitTag>();
    });

    IDistributedCounterFactory factory = context.CreateFactory();
    IDistributedCounter<RateLimitTag> counter = factory.Create<RateLimitTag>();
    CancellationToken ct = CancellationToken.None;

    // Act 1: Consume limit with 30-second window
    await counter.TryIncrementAsync("user_1", limit: 5, amount: 5, expiry: CounterExpiry.FromSeconds(30), ct);

    // Act 2: Advance time past the expiration window
    timeProvider.Advance(TimeSpan.FromSeconds(31));

    // Act 3: Try incrementing again in the new window
    CounterLimitResult newWindowResult = await counter.TryIncrementAsync(
        "user_1", 
        limit: 5, 
        amount: 2, 
        expiry: CounterExpiry.FromSeconds(30), 
        ct);

    // Assert: Quota reset, new increment allowed
    Assert.True(newWindowResult.IsAllowed);
    Assert.Equal(2, newWindowResult.CurrentValue);
}
```

---

### 3. Simulating Storage Failures

Test resilience and error handling paths in your application by simulating storage exceptions:

```csharp
[Fact]
public async Task WhenStorageFails_ServiceHandlesGracefully() {
    // Arrange
    DistributedCounterTestContext context = new();
    context.Storage.SimulateAtomicIncrementFailure(new TimeoutException("Redis connection timed out"));

    IDistributedCounterFactory factory = context.CreateFactory();
    IDistributedCounter counter = factory.Create("test_metric");

    // Act & Assert
    await Assert.ThrowsAsync<TimeoutException>(async () => {
        await counter.IncrementAsync(1, CounterExpiry.Infinite, CancellationToken.None);
    });
}
```

---

### 4. Integration Testing with ASP.NET Core `WebApplicationFactory`

Substitute production Redis with `FakeCounterStorage` in integration test fixtures:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Testing;

public sealed class CustomApiFactory : WebApplicationFactory<Program> {
    public FakeCounterStorage FakeStorage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.ConfigureServices(services => {
            // Replaces registered ICounterStorage with the inspectable fake instance
            services.AddDistributedCounter(counter => {
                counter.UseFakeStorage(FakeStorage);
            });
        });
    }
}
```

---

## License

This project is licensed under the MIT License.