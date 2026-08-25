using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.Resilience;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ConcurrencyStress")]
public sealed class CircuitBreakerConcurrencyStressTests {

    private static (ConsecutiveFailuresCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSut(
        int failureThreshold = 5,
        TimeSpan? breakDuration = null) {

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        CircuitBreakerOptions options = new() {
            FailureThreshold = failureThreshold,
            BreakDuration = breakDuration ?? TimeSpan.FromMinutes(2)
        };

        ConsecutiveFailuresCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    [Fact]
    public async Task OnFailureAsync_Under100ConcurrentFailures_TripsCleanlyToOpenWithoutStateCorruption() {
        // Arrange: 100 tasks failing simultaneously on a fresh key
        (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 5);
        const string key = "endpoint-concurrency-flood";

        Task[] tasks = Enumerable.Range(0, 100).Select(_ =>
            breaker.OnFailureAsync(key, TestContext.Current.CancellationToken).AsTask()
        ).ToArray();

        await Task.WhenAll(tasks);

        // Assert: Circuit must be OPEN
        CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
        Assert.False(decision.IsAllowed);
        Assert.Equal(CircuitState.Open, decision.State);
        Assert.NotNull(decision.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenInHalfOpen_AllowsExactlyOneProbe_AndDeniesRemaining50ConcurrentCallers() {
        // Arrange: Threshold = 1, BreakDuration = 30s
        (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromSeconds(30));

        const string key = "endpoint-half-open-race";

        // 1. Trip circuit
        await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
        Assert.False((await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).IsAllowed);

        // 2. Advance time past break duration (31s) -> Enters Half-Open
        timeProvider.Advance(TimeSpan.FromSeconds(31));

        // 3. 50 concurrent requests bombard the circuit in the exact same millisecond!
        ConcurrentBag<CircuitExecutionDecision> decisions = [];

        Task[] tasks = Enumerable.Range(0, 50).Select(async _ => {
            CircuitExecutionDecision d = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            decisions.Add(d);
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert: Exactly 1 caller must receive HalfOpenProbe; remaining 49 must receive Denied!
        int probeCount = decisions.Count(d => d.IsAllowed && d.State == CircuitState.HalfOpen);
        int deniedCount = decisions.Count(d => !d.IsAllowed && d.State == CircuitState.Open);

        Assert.Equal(1, probeCount);
        Assert.Equal(49, deniedCount);
    }
}