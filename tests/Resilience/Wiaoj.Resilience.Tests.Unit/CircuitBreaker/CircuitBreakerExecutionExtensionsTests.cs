using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
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

    public sealed class TheArgumentValidation {
        [Fact]
        public async Task ExecuteAsync_Throws_OnNullCircuitBreaker() {
            ICircuitBreaker breaker = null!;
            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                breaker.ExecuteAsync("key", ct => ValueTask.FromResult("test"), TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_Throws_OnInvalidKey(string? invalidKey) {
            (ICircuitBreaker breaker, _) = CreateSut();
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                breaker.ExecuteAsync(invalidKey!, ct => ValueTask.FromResult("test"), TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task ExecuteAsync_Throws_OnNullOperation() {
            (ICircuitBreaker breaker, _) = CreateSut();
            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                breaker.ExecuteAsync<string>("key", null!, TestContext.Current.CancellationToken).AsTask());
        }
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

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOperationThrows_RecordsFailureAndRethrowsOriginalException() {
            (ICircuitBreaker breaker, _) = CreateSut(failureThreshold: 2);
            const string key = "service-http-call";

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new HttpRequestException("503 Service Unavailable"), TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new HttpRequestException("503 Service Unavailable"), TestContext.Current.CancellationToken).AsTask());

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
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                breaker.ExecuteAsync(key, async ct => {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                    return "Never";
                }, cts.Token).AsTask());

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

        [Fact]
        public async Task ExecuteAsync_NonGeneric_WhenThrows_RecordsFailure() {
            (ICircuitBreaker breaker, _) = CreateSut(failureThreshold: 1);
            const string key = "service-void-fail";

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync(key, ct => throw new InvalidOperationException("Fatal"), TestContext.Current.CancellationToken).AsTask());

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
        }
    }
}