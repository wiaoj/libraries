using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.CircuitBreaker;
using Wiaoj.Webhooks.Resilience;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "FaultAndBoundary")]
public sealed class CircuitBreakerFaultAndBoundaryTests {

    private static DistributedCircuitBreakerStore CreateStore() {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDistributedCounter(c => c.UseInMemory());
        IDistributedCounterFactory counterFactory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

        return new DistributedCircuitBreakerStore(counterFactory, TimeProvider.System, NullLogger<DistributedCircuitBreakerStore>.Instance);
    }

    public sealed class TheCancellationPropagation {
        [Fact]
        public async Task CanExecuteAsync_ThrowsOperationCanceledException_WhenCancelled() {
            DistributedCircuitBreakerStore store = CreateStore();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.CanExecuteAsync("endpoint-cancel", cts.Token).AsTask());
        }

        [Fact]
        public async Task Middleware_InvokeAsync_ThrowsOperationCanceledException_WhenTokenIsCancelled() {
            DistributedCircuitBreakerStore store = CreateStore();
            CircuitBreakerOptions options = new();
            CircuitBreakerMiddleware middleware = new(store, options, NullLogger<CircuitBreakerMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                middleware.InvokeAsync(context, static (_, _) => Task.CompletedTask, cts.Token));
        }
    }

    public sealed class TheExtremeDurationsAndKeys {
        [Fact]
        public async Task Store_HandlesExtremeLongBreakDuration_WithoutIntegerOverflow() {
            FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
            ServiceCollection services = new();
            services.AddSingleton<TimeProvider>(timeProvider);
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory counterFactory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            DistributedCircuitBreakerStore store = new(counterFactory, timeProvider, NullLogger<DistributedCircuitBreakerStore>.Instance);

            // 30 days break duration
            CircuitBreakerOptions options = new() {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromDays(30)
            };

            await store.RecordFailureAsync("ep-extreme-duration", options, TestContext.Current.CancellationToken);

            CircuitExecutionDecision decision = await store.CanExecuteAsync("ep-extreme-duration", TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.NotNull(decision.RetryAfter);
            Assert.Equal(TimeSpan.FromDays(30), decision.RetryAfter.Value);
        }

        [Fact]
        public async Task Store_HandlesSpecialCharactersAndUnicodeInEndpointKey() {
            DistributedCircuitBreakerStore store = CreateStore();
            const string complexKey = "ep_müşteri_öçşğü_🚀_tenant:42";
            CircuitBreakerOptions options = new() { FailureThreshold = 1, BreakDuration = TimeSpan.FromSeconds(30) };

            await store.RecordFailureAsync(complexKey, options, TestContext.Current.CancellationToken);

            CircuitExecutionDecision decision = await store.CanExecuteAsync(complexKey, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.False(decision.IsAllowed);
        }
    }
}