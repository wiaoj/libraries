using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.Resilience;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ExecutionExtensions")]
public sealed class CircuitBreakerExecutionExtensionsTests {

    private static (ICircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSut(int failureThreshold = 2) {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        CircuitBreakerOptions options = new() {
            FailureThreshold = failureThreshold,
            BreakDuration = TimeSpan.FromSeconds(30)
        };

        ConsecutiveFailuresCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    public sealed class TheGenericExecution {
        [Fact]
        public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsValueAndReportsSuccess() {
            (ICircuitBreaker breaker, _) = CreateSut();
            const string key = "service-db-read";

            string result = await breaker.ExecuteAsync(key, async ct => {
                await Task.Yield();
                return "Order_Payload_777";
            }, TestContext.Current.CancellationToken);

            Assert.Equal("Order_Payload_777", result);

            // Circuit must remain closed
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOperationThrows_RecordsFailureAndRethrowsOriginalException() {
            (ICircuitBreaker breaker, _) = CreateSut(failureThreshold: 2);
            const string key = "service-http-call";

            // 1. First failure
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new HttpRequestException("503 Service Unavailable"), TestContext.Current.CancellationToken).AsTask());

            // 2. Second failure -> Hits threshold of 2
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new HttpRequestException("503 Service Unavailable"), TestContext.Current.CancellationToken).AsTask());

            // 3. Third call -> Circuit is now OPEN! Must throw CircuitBreakerOpenException without invoking user delegate!
            bool delegateExecuted = false;
            CircuitBreakerOpenException openEx = await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
                breaker.ExecuteAsync<string>(key, ct => {
                    delegateExecuted = true;
                    return ValueTask.FromResult("Should not run");
                }, TestContext.Current.CancellationToken).AsTask());

            Assert.False(delegateExecuted);
            Assert.Equal(key, openEx.Key);
            Assert.NotNull(openEx.RetryAfter);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCallerCancels_DoesNotRecordFailure() {
            (ICircuitBreaker breaker, _) = CreateSut(failureThreshold: 1);
            const string key = "service-cancel-test";

            using CancellationTokenSource cts = new();
            cts.Cancel(); // Token is already cancelled by the caller

            // Act: Caller cancellation should throw OperationCanceledException
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                breaker.ExecuteAsync(key, async ct => {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                    return "Never";
                }, cts.Token).AsTask());

            // Assert: Caller cancellation is NOT a downstream failure! Circuit must remain CLOSED.
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }

    public sealed class TheNonGenericExecution {
        [Fact]
        public async Task ExecuteAsync_NonGeneric_ExecutesActionSuccessfully() {
            (ICircuitBreaker breaker, _) = CreateSut();
            const string key = "service-void-action";
            bool actionExecuted = false;

            await breaker.ExecuteAsync(key, async ct => {
                await Task.Yield();
                actionExecuted = true;
            }, TestContext.Current.CancellationToken);

            Assert.True(actionExecuted);
        }
    }
}