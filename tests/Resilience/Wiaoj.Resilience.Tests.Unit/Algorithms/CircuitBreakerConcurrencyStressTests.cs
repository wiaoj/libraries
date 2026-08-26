using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ConcurrencyStress")]
public sealed class CircuitBreakerConcurrencyStressTests {

    private static (ConsecutiveFailuresCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateConsecutiveSut(
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

    private static (SamplingWindowCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSamplingSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        SamplingWindowCircuitBreakerOptions options = new() {
            FailureRateThreshold = 0.5,
            MinimumThroughput = 50,
            PermittedNumberOfCallsInHalfOpenState = 5,
            SamplingWindow = TimeSpan.FromMinutes(1),
            BreakDuration = TimeSpan.FromMinutes(2)
        };

        SamplingWindowCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<SamplingWindowCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    [Fact]
    public async Task OnFailureAsync_Under100ConcurrentFailures_TripsCleanlyToOpenWithoutStateCorruption() {
        (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateConsecutiveSut(failureThreshold: 5);
        const string key = "endpoint-concurrency-flood";

        Task[] tasks = Enumerable.Range(0, 100).Select(_ =>
            breaker.OnFailureAsync(key, TestContext.Current.CancellationToken).AsTask()
        ).ToArray();

        await Task.WhenAll(tasks);

        CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
        Assert.False(decision.IsAllowed);
        Assert.Equal(CircuitState.Open, decision.State);
        Assert.NotNull(decision.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenInHalfOpen_AllowsExactlyOneProbe_AndDeniesRemaining50ConcurrentCallers() {
        (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateConsecutiveSut(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromSeconds(30));

        const string key = "endpoint-half-open-race";

        await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
        Assert.False((await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).IsAllowed);

        timeProvider.Advance(TimeSpan.FromSeconds(31));

        ConcurrentBag<CircuitExecutionDecision> decisions = [];

        Task[] tasks = Enumerable.Range(0, 50).Select(async _ => {
            CircuitExecutionDecision d = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            decisions.Add(d);
        }).ToArray();

        await Task.WhenAll(tasks);

        int probeCount = decisions.Count(d => d.IsAllowed && d.State == CircuitState.HalfOpen);
        int deniedCount = decisions.Count(d => !d.IsAllowed && d.State == CircuitState.Open);

        Assert.Equal(1, probeCount);
        Assert.Equal(49, deniedCount);
    }

    [Fact]
    public async Task SamplingWindow_Under100ConcurrentRequests_CalculatesAccurateErrorRateAndTrips() {
        (SamplingWindowCircuitBreaker breaker, _) = CreateSamplingSut();
        const string key = "sampling-concurrency-race";

        // 60 failures, 40 successes simultaneously = 60% failure rate >= 50% threshold
        Task[] failureTasks = Enumerable.Range(0, 60).Select(_ =>
            breaker.OnFailureAsync(key, TestContext.Current.CancellationToken).AsTask()
        ).ToArray();

        Task[] successTasks = Enumerable.Range(0, 40).Select(_ =>
            breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken).AsTask()
        ).ToArray();

        await Task.WhenAll(failureTasks.Concat(successTasks));

        CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
        Assert.False(decision.IsAllowed);
        Assert.Equal(CircuitState.Open, decision.State);
    }
}