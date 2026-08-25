using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ExecutionWrapper")]
public sealed class CircuitBreakerExecutionExtensionsTests {

    private static (ICircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        CircuitBreakerOptions options = new() {
            FailureThreshold = 2,
            BreakDuration = TimeSpan.FromSeconds(30)
        };

        ConsecutiveFailuresCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsResultAndRecordsSuccess() {
        (ICircuitBreaker breaker, _) = CreateSut();
        const string key = "db-read-orders";

        // Act: Execute an operation through the circuit breaker
        string result = await breaker.ExecuteAsync(key, async ct => {
            await Task.Yield();
            return "Order_Data_123";
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Order_Data_123", result);

        CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
        Assert.True(decision.IsAllowed);
        Assert.Equal(CircuitState.Closed, decision.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationThrows_RecordsFailureAndReThrowsException() {
        (ICircuitBreaker breaker, _) = CreateSut();
        const string key = "http-remote-service";

        // Act 1: First failing call
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            breaker.ExecuteAsync<string>(key, ct => throw new HttpRequestException("Connection refused"), TestContext.Current.CancellationToken).AsTask());

        // Act 2: Second failing call (Reaches threshold of 2)
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            breaker.ExecuteAsync<string>(key, ct => throw new HttpRequestException("Connection refused"), TestContext.Current.CancellationToken).AsTask());

        // Act 3: Third call -> Throws CircuitBreakerOpenException immediately without calling delegate!
        bool delegateExecuted = false;
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            breaker.ExecuteAsync(key, ct => {
                delegateExecuted = true;
                return ValueTask.FromResult("Should not execute");
            }, TestContext.Current.CancellationToken).AsTask());

        Assert.False(delegateExecuted);
    }
}