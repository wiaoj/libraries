using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.CircuitBreaker;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Resilience;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "PipelineChaos")]
public sealed class CircuitBreakerPipelineChaosTests {

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

    public sealed class TheErrorDiscriminationAndPipelineFlow {
        [Fact]
        public async Task Pipeline_WhenSocketExceptionOrTimeoutOccurs_ClassifiesAsTransientAndTripsCircuit() {
            // Arrange: Socket reset or DNS timeout (null status code transient failure)
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            CircuitBreakerOptions options = new() { FailureThreshold = 2, BreakDuration = TimeSpan.FromMinutes(5) };
            CircuitBreakerMiddleware middleware = new(store, options, NullLogger<CircuitBreakerMiddleware>.Instance);

            FakeWebhookDeliverer deliverer = new(
                WebhookDeliveryResult.Transient("Connection refused by target", new TimeoutException()),
                WebhookDeliveryResult.Transient("Connection reset by peer", new HttpRequestException("TCP Drop")));

            WebhookPipelineRunner runner = new([middleware], deliverer, TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance);
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

            // 1st network failure
            await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);
            // 2nd network failure -> Reaches threshold
            await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);

            // Assert: Circuit is tripped to OPEN by socket drops
            CircuitExecutionDecision decision = await store.CanExecuteAsync(endpoint.Id.Value, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, decision.State);
        }

        [Fact]
        public async Task Pipeline_FastFailResult_ContainsExactRetryAfterAnd503StatusCode() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            CircuitBreakerOptions options = new() {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromSeconds(45)
            };
            CircuitBreakerMiddleware middleware = new(store, options, NullLogger<CircuitBreakerMiddleware>.Instance);

            FakeWebhookDeliverer deliverer = new(
                WebhookDeliveryResult.Transient("503 Glitch", 503),
                WebhookDeliveryResult.Success(200)); // Should never be reached

            WebhookPipelineRunner runner = new([middleware], deliverer, TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance);
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

            // Trip circuit on attempt 1
            await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);

            // Attempt 2 while OPEN
            WebhookDeliveryAttempt attempt2 = await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(attempt2.IsSuccess);
            WebhookDeliveryResult.TransientFailure fastFail = Assert.IsType<WebhookDeliveryResult.TransientFailure>(attempt2.Result);
            Assert.Equal(503, fastFail.StatusCode);
            Assert.NotNull(fastFail.RetryAfter);
            Assert.True(fastFail.RetryAfter.Value <= TimeSpan.FromSeconds(45));
            Assert.Contains("Circuit breaker is OPEN", fastFail.ErrorMessage);
        }

        [Fact]
        public async Task Pipeline_WhenClient4xxFollowedByTransient5xx_OnlyCounts5xxTowardThreshold() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            CircuitBreakerOptions options = new() { FailureThreshold = 2, BreakDuration = TimeSpan.FromMinutes(1) };
            CircuitBreakerMiddleware middleware = new(store, options, NullLogger<CircuitBreakerMiddleware>.Instance);

            FakeWebhookDeliverer deliverer = new(
                WebhookDeliveryResult.Permanent("400 Bad Request", 400),
                WebhookDeliveryResult.Permanent("404 Not Found", 404),
                WebhookDeliveryResult.Transient("500 Internal Error", 500)); // Only 1 transient error

            WebhookPipelineRunner runner = new([middleware], deliverer, TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance);
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

            // Run all 3 requests
            await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);
            await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);
            await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint), TestContext.Current.CancellationToken);

            // Assert: Since 4xx errors are excluded, transient count is only 1 (< threshold of 2) -> Circuit remains CLOSED
            CircuitExecutionDecision decision = await store.CanExecuteAsync(endpoint.Id.Value, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Closed, decision.State);
            Assert.True(decision.IsAllowed);
        }
    }
}