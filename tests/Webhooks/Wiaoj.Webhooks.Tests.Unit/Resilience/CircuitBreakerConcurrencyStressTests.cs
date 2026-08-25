using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.CircuitBreaker;
using Wiaoj.Webhooks.Resilience;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ConcurrencyStress")]
public sealed class CircuitBreakerConcurrencyStressTests {

    private static (DistributedCircuitBreakerStore Store, FakeTimeProvider TimeProvider) CreateSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        DistributedCircuitBreakerStore store = new(counterFactory, timeProvider, NullLogger<DistributedCircuitBreakerStore>.Instance);
        return (store, timeProvider);
    }

    public sealed class TheHighVolumeContention {
        [Fact]
        public async Task InvokeAsync_Under100ConcurrentFailingThreads_TripsCleanlyAtThreshold() {
            // Arrange: 100 concurrent workers competing against an endpoint that fails on every request
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            CircuitBreakerOptions options = new() {
                FailureThreshold = 5,
                BreakDuration = TimeSpan.FromMinutes(2)
            };
            CircuitBreakerMiddleware middleware = new(store, options, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

            int downstreamExecutedCount = 0;
            WebhookDelegate downstream = async (ctx, ct) => {
                Interlocked.Increment(ref downstreamExecutedCount);
                await Task.Delay(15);
                ctx.SetResult(WebhookDeliveryResult.Transient("503 Glitch", 503));
            };

            // Act: 100 requests flood the middleware simultaneously
            Task[] tasks = Enumerable.Range(0, 100).Select(async _ => {
                WebhookDeliveryContext ctx = WebhookTestFactory.CreateContext(endpoint);
                await middleware.InvokeAsync(ctx, downstream, TestContext.Current.CancellationToken);
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: The circuit breaker must have shielded the target, allowing significantly fewer than 100 downstream calls
            Assert.True(downstreamExecutedCount < 100, $"Downstream executed {downstreamExecutedCount} times. Expected circuit breaker to fast-fail excess calls.");

            // Final state must be firmly OPEN
            CircuitExecutionDecision decision = await store.CanExecuteAsync(endpoint.Id.Value, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.False(decision.IsAllowed);
        }

        [Fact]
        public async Task InvokeAsync_UnderMultiEndpointParallelFlood_MaintainsIsolatedStateForEachEndpoint() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            CircuitBreakerOptions options = new() { FailureThreshold = 3, BreakDuration = TimeSpan.FromMinutes(1) };
            CircuitBreakerMiddleware middleware = new(store, options, NullLogger<CircuitBreakerMiddleware>.Instance);

            // 5 distinct endpoints: 3 failing, 2 healthy
            string[] failingEndpointIds = ["ep-fail-1", "ep-fail-2", "ep-fail-3"];
            string[] healthyEndpointIds = ["ep-ok-1", "ep-ok-2"];

            ConcurrentDictionary<string, int> executionCounts = new();

            WebhookDelegate handler = (ctx, ct) => {
                string id = ctx.Endpoint.Id.Value;
                executionCounts.AddOrUpdate(id, 1, (_, current) => current + 1);

                if(id.StartsWith("ep-fail", StringComparison.Ordinal)) {
                    ctx.SetResult(WebhookDeliveryResult.Transient("500 Server Down", 500));
                }
                else {
                    ctx.SetResult(WebhookDeliveryResult.Success(200));
                }
                return Task.CompletedTask;
            };

            // Act: Run 150 tasks (30 per endpoint) concurrently
            Task[] tasks = Enumerable.Range(0, 150).Select(async i => {
                string targetId = i % 2 == 0
                    ? failingEndpointIds[i % failingEndpointIds.Length]
                    : healthyEndpointIds[i % healthyEndpointIds.Length];

                WebhookEndpoint ep = WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId(targetId));
                WebhookDeliveryContext ctx = WebhookTestFactory.CreateContext(ep);

                await middleware.InvokeAsync(ctx, handler, TestContext.Current.CancellationToken);
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: All failing endpoints must be OPEN
            foreach(string failId in failingEndpointIds) {
                CircuitExecutionDecision decision = await store.CanExecuteAsync(failId, TestContext.Current.CancellationToken);
                Assert.Equal(CircuitState.Open, decision.State);
            }

            // Assert: All healthy endpoints must remain CLOSED
            foreach(string okId in healthyEndpointIds) {
                CircuitExecutionDecision decision = await store.CanExecuteAsync(okId, TestContext.Current.CancellationToken);
                Assert.Equal(CircuitState.Closed, decision.State);
                Assert.True(decision.IsAllowed);
            }
        }
    }
}