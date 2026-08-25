using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Resilience;
using Wiaoj.Webhooks.Resilience;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "CircuitBreakerMiddleware")]
public sealed class CircuitBreakerMiddlewareTests {

    private sealed class SpyCircuitBreaker : ICircuitBreaker {
        public CircuitExecutionDecision DecisionToReturn { get; set; } = CircuitExecutionDecision.Allowed();
        public int TryAcquireCount { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }
        public string? LastKey { get; private set; }

        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
            this.TryAcquireCount++;
            this.LastKey = key;
            return ValueTask.FromResult(this.DecisionToReturn);
        }

        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
            this.SuccessCount++;
            this.LastKey = key;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
            this.FailureCount++;
            this.LastKey = key;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class TheClosedStateAndSuccessFlow {
        [Fact]
        public async Task InvokeAsync_WhenCircuitIsAllowed_InvokesDownstreamAndReportsSuccess() {
            SpyCircuitBreaker breaker = new() {
                DecisionToReturn = CircuitExecutionDecision.Allowed()
            };
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            bool downstreamInvoked = false;
            WebhookDelegate downstream = (ctx, ct) => {
                downstreamInvoked = true;
                ctx.SetResult(WebhookDeliveryResult.Success(200));
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, downstream, TestContext.Current.CancellationToken);

            Assert.True(downstreamInvoked);
            Assert.Equal(1, breaker.TryAcquireCount);
            Assert.Equal(1, breaker.SuccessCount);
            Assert.Equal(0, breaker.FailureCount);
            Assert.Equal(context.Endpoint.Id.Value, breaker.LastKey);
        }
    }

    public sealed class TheOpenStateFastFailFlow {
        [Fact]
        public async Task InvokeAsync_WhenCircuitIsOpen_FastFailsAndDoesNotInvokeDownstream() {
            TimeSpan retryAfter = TimeSpan.FromSeconds(45);
            SpyCircuitBreaker breaker = new() {
                DecisionToReturn = CircuitExecutionDecision.Denied(retryAfter)
            };
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            bool downstreamInvoked = false;
            WebhookDelegate downstream = (ctx, ct) => {
                downstreamInvoked = true;
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, downstream, TestContext.Current.CancellationToken);

            // Assert: Downstream must NEVER be reached when circuit is open (Zero Network I/O)
            Assert.False(downstreamInvoked);
            Assert.Equal(1, breaker.TryAcquireCount);
            Assert.Equal(0, breaker.SuccessCount);
            Assert.Equal(0, breaker.FailureCount);

            // Assert: Context must be populated with a Transient failure tagged as CircuitBreakerOpen
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.TransientFailure transient = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
            Assert.False(transient.IsSuccess);
            Assert.Equal(503, transient.StatusCode);
            Assert.Equal(retryAfter, transient.RetryAfter);
            Assert.Equal(TransientFailureReason.CircuitBreakerOpen, transient.Reason);
            Assert.Contains("Circuit breaker is OPEN", transient.ErrorMessage);
        }
    }

    public sealed class TheOutcomeDiscrimination {
        [Fact]
        public async Task InvokeAsync_WhenDownstreamReturnsTransientFailure_ReportsFailureToCircuitBreaker() {
            SpyCircuitBreaker breaker = new() {
                DecisionToReturn = CircuitExecutionDecision.Allowed()
            };
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            WebhookDelegate downstream = (ctx, ct) => {
                ctx.SetResult(WebhookDeliveryResult.Transient("503 Service Unavailable", 503));
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, downstream, TestContext.Current.CancellationToken);

            Assert.Equal(1, breaker.FailureCount);
            Assert.Equal(0, breaker.SuccessCount);
        }

        [Fact]
        public async Task InvokeAsync_WhenDownstreamReturnsPermanent4xxFailure_DoesNotReportFailureToBreaker() {
            SpyCircuitBreaker breaker = new() {
                DecisionToReturn = CircuitExecutionDecision.Allowed()
            };
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            WebhookDelegate downstream = (ctx, ct) => {
                ctx.SetResult(WebhookDeliveryResult.Permanent("404 Not Found", 404, PermanentFailureReason.EndpointNotFound));
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, downstream, TestContext.Current.CancellationToken);

            // Assert: Permanent client errors must remain neutral and NEVER trip the circuit breaker
            Assert.Equal(0, breaker.FailureCount);
            Assert.Equal(0, breaker.SuccessCount);
        }

        [Fact]
        public async Task InvokeAsync_WhenDownstreamThrowsUnexpectedException_ReportsFailureAndRethrows() {
            SpyCircuitBreaker breaker = new() {
                DecisionToReturn = CircuitExecutionDecision.Allowed()
            };
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            WebhookDelegate downstream = (ctx, ct) => throw new HttpRequestException("TCP Connection Reset");

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                middleware.InvokeAsync(context, downstream, TestContext.Current.CancellationToken));

            Assert.Equal(1, breaker.FailureCount);
        }
    }

    public sealed class TheCancellationGuards {
        [Fact]
        public async Task InvokeAsync_WhenCallerCancels_RethrowsWithoutReportingFailure() {
            SpyCircuitBreaker breaker = new() {
                DecisionToReturn = CircuitExecutionDecision.Allowed()
            };
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            using CancellationTokenSource cts = new();
            cts.Cancel(); // Caller cancellation token triggered

            WebhookDelegate downstream = (ctx, ct) => {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                middleware.InvokeAsync(context, downstream, cts.Token));

            // Assert: Caller cancellation is not a service failure -> Must not increment failure count
            Assert.Equal(0, breaker.FailureCount);
        }
    }

    public sealed class TheConstructorAndGuards {
        [Fact]
        public void Constructor_Throws_WhenParametersAreNull() {
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new CircuitBreakerMiddleware(null!, NullLogger<CircuitBreakerMiddleware>.Instance));

            SpyCircuitBreaker breaker = new();
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new CircuitBreakerMiddleware(breaker, null!));
        }

        [Fact]
        public async Task InvokeAsync_Throws_WhenContextOrNextIsNull() {
            SpyCircuitBreaker breaker = new();
            CircuitBreakerMiddleware middleware = new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                middleware.InvokeAsync(null!, (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken));

            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                middleware.InvokeAsync(context, null!, TestContext.Current.CancellationToken));
        }
    }
}