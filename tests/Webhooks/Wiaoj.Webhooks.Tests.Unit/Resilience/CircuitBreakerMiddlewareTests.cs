using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience;
using Wiaoj.Webhooks.Resilience;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "CircuitBreakerMiddleware")]
public sealed class CircuitBreakerMiddlewareTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Records what the middleware reports and answers <c>TryAcquire</c> from a script.</summary>
    private sealed class RecordingBreaker : ICircuitBreaker {
        public CircuitExecutionDecision Decision { get; set; } = CircuitExecutionDecision.Allowed();
        public List<string> Acquired { get; } = [];
        public List<string> Successes { get; } = [];
        public List<string> Failures { get; } = [];

        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
            this.Acquired.Add(key);
            return ValueTask.FromResult(this.Decision);
        }

        public ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CircuitState.Closed);

        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
            this.Successes.Add(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
            this.Failures.Add(key);
            return ValueTask.CompletedTask;
        }
    }

    private static CircuitBreakerMiddleware Create(ICircuitBreaker breaker) =>
        new(breaker, NullLogger<CircuitBreakerMiddleware>.Instance);

    private static WebhookDelegate Returning(WebhookDeliveryResult result, Action? onCalled = null) => (context, _) => {
        onCalled?.Invoke();
        context.SetResult(result);
        return Task.CompletedTask;
    };

    public sealed class TheDecision {
        [Fact]
        public async Task Allowed_AndDelivered_ReportsSuccessForTheEndpoint() {
            RecordingBreaker breaker = new();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            bool called = false;

            await Create(breaker).InvokeAsync(context, Returning(WebhookDeliveryResult.Success(), () => called = true), Ct);

            Assert.True(called);
            Assert.Equal([context.Endpoint.Id.Value], breaker.Acquired);
            Assert.Equal([context.Endpoint.Id.Value], breaker.Successes);
            Assert.Empty(breaker.Failures);
        }

        [Fact]
        public async Task Denied_DoesNotRunTheDelivery_AndAnswersCircuitBroken() {
            RecordingBreaker breaker = new() { Decision = CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(30)) };
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            bool called = false;

            await Create(breaker).InvokeAsync(context, Returning(WebhookDeliveryResult.Success(), () => called = true), Ct);

            Assert.False(called);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.TransientFailure failure = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
            Assert.Equal(TransientFailureReason.CircuitBreakerOpen, failure.Reason);
            Assert.Equal(TimeSpan.FromSeconds(30), failure.RetryAfter);
            Assert.Empty(breaker.Successes);
            Assert.Empty(breaker.Failures);
        }

        [Fact]
        public async Task Denied_WithoutAUsableRetryAfter_FallsBackToOneSecond() {
            RecordingBreaker breaker = new() { Decision = CircuitExecutionDecision.Denied(TimeSpan.Zero) };
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Create(breaker).InvokeAsync(context, Returning(WebhookDeliveryResult.Success()), Ct);

            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            Assert.Equal(TimeSpan.FromSeconds(1), Assert.IsType<WebhookDeliveryResult.TransientFailure>(result).RetryAfter);
        }

        [Fact]
        public async Task HalfOpenProbe_IsDelivered_AndReportsItsOutcome() {
            RecordingBreaker breaker = new() { Decision = CircuitExecutionDecision.HalfOpenProbe() };
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Create(breaker).InvokeAsync(context, Returning(WebhookDeliveryResult.Success()), Ct);

            Assert.Single(breaker.Successes);
        }

        [Fact]
        public async Task EachEndpointIsAKeyOfItsOwn() {
            RecordingBreaker breaker = new();
            WebhookDeliveryContext first = WebhookTestFactory.CreateContext(WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId("endpoint-a")));
            WebhookDeliveryContext second = WebhookTestFactory.CreateContext(WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId("endpoint-b")));

            await Create(breaker).InvokeAsync(first, Returning(WebhookDeliveryResult.Success()), Ct);
            await Create(breaker).InvokeAsync(second, Returning(WebhookDeliveryResult.Success()), Ct);

            Assert.Equal(["endpoint-a", "endpoint-b"], breaker.Acquired);
        }
    }

    public sealed class TheOutcome {
        [Fact]
        public async Task ATransientFailure_CountsAgainstTheEndpoint() {
            RecordingBreaker breaker = new();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Create(breaker).InvokeAsync(context, Returning(WebhookDeliveryResult.Transient("502", 502)), Ct);

            Assert.Single(breaker.Failures);
            Assert.Empty(breaker.Successes);
        }

        [Fact]
        public async Task AThrownException_CountsAsAFailure_AndIsRethrown() {
            RecordingBreaker breaker = new();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Assert.ThrowsAsync<InvalidOperationException>(() => Create(breaker).InvokeAsync(
                context, (_, _) => throw new InvalidOperationException("boom"), Ct));

            Assert.Single(breaker.Failures);
        }

        [Fact]
        public async Task ACancelledDelivery_IsNotTheEndpointsFault() {
            RecordingBreaker breaker = new();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(breaker).InvokeAsync(
                context, (_, token) => throw new OperationCanceledException(token), cts.Token));

            Assert.Empty(breaker.Failures);
        }

        [Fact]
        public async Task APermanentFailure_NeitherOpensNorClosesTheCircuit() {
            // A 4xx the receiver answered on purpose says the endpoint is up.
            RecordingBreaker breaker = new();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Create(breaker).InvokeAsync(context, Returning(WebhookDeliveryResult.Permanent("rejected", 400)), Ct);

            Assert.Empty(breaker.Failures);
            Assert.Empty(breaker.Successes);
        }

        [Theory]
        [InlineData(TransientFailureReason.RateLimitThrottled)]
        [InlineData(TransientFailureReason.CircuitBreakerOpen)]
        public async Task ALocalThrottle_IsNotAFailureOfTheEndpoint(TransientFailureReason reason) {
            // The local limiter or another breaker declined to send; the remote was never asked.
            RecordingBreaker breaker = new();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            WebhookDeliveryResult throttled = new WebhookDeliveryResult.TransientFailure("declined locally", null, TimeSpan.FromSeconds(1), reason);

            await Create(breaker).InvokeAsync(context, Returning(throttled), Ct);

            Assert.Empty(breaker.Failures);
        }
    }

    /// <summary>The real consecutive-failure breaker, driven by a fake clock: no waiting.</summary>
    public sealed class TheRealBreaker {
        private const int Threshold = 3;
        private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

        private static (CircuitBreakerMiddleware Middleware, FakeTimeProvider Time) Build() {
            FakeTimeProvider time = new();
            ServiceCollection services = new();
            services.AddSingleton<TimeProvider>(time);
            services.AddDistributedCounter(dc => dc.UseInMemory());
            services.AddWiaojResilience(resilience => resilience.UseDefaultConsecutiveBreaker(options => {
                options.FailureThreshold = Threshold;
                options.BreakDuration = BreakDuration;
            }));

            ServiceProvider provider = services.BuildServiceProvider();
            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreakerFactory>().Create();
            return (Create(breaker), time);
        }

        private static async Task<(bool Delivered, WebhookDeliveryResult? Result)> DeliverAsync(
            CircuitBreakerMiddleware middleware, WebhookDeliveryContext context, WebhookDeliveryResult outcome) {
            bool delivered = false;
            await middleware.InvokeAsync(context, Returning(outcome, () => delivered = true), Ct);
            context.TryGetResult(out WebhookDeliveryResult? result);
            return (delivered, result);
        }

        private static WebhookDeliveryContext ContextFor(string endpoint) =>
            WebhookTestFactory.CreateContext(WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId(endpoint)));

        [Fact]
        public async Task OpensAfterTheThresholdOfConsecutiveFailures_AndStopsDelivering() {
            (CircuitBreakerMiddleware middleware, _) = Build();

            for(int i = 0; i < Threshold; i++) {
                (bool delivered, _) = await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Transient("502", 502));
                Assert.True(delivered);
            }

            (bool deliveredAfterOpen, WebhookDeliveryResult? result) =
                await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Success());

            Assert.False(deliveredAfterOpen);
            Assert.Equal(TransientFailureReason.CircuitBreakerOpen, Assert.IsType<WebhookDeliveryResult.TransientFailure>(result).Reason);
        }

        [Fact]
        public async Task StaysClosedBelowTheThreshold() {
            (CircuitBreakerMiddleware middleware, _) = Build();

            for(int i = 0; i < Threshold - 1; i++) {
                await DeliverAsync(middleware, ContextFor("flaky"), WebhookDeliveryResult.Transient("502", 502));
            }

            (bool delivered, _) = await DeliverAsync(middleware, ContextFor("flaky"), WebhookDeliveryResult.Success());
            Assert.True(delivered);
        }

        [Fact]
        public async Task ASuccessResetsTheCount() {
            (CircuitBreakerMiddleware middleware, _) = Build();

            for(int round = 0; round < 3; round++) {
                for(int i = 0; i < Threshold - 1; i++) {
                    await DeliverAsync(middleware, ContextFor("recovering"), WebhookDeliveryResult.Transient("502", 502));
                }
                await DeliverAsync(middleware, ContextFor("recovering"), WebhookDeliveryResult.Success());
            }

            (bool delivered, _) = await DeliverAsync(middleware, ContextFor("recovering"), WebhookDeliveryResult.Success());
            Assert.True(delivered);
        }

        [Fact]
        public async Task OneDownEndpointDoesNotStopTheOthers() {
            (CircuitBreakerMiddleware middleware, _) = Build();
            for(int i = 0; i < Threshold; i++) {
                await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Transient("502", 502));
            }

            (bool downDelivered, _) = await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Success());
            (bool healthyDelivered, _) = await DeliverAsync(middleware, ContextFor("healthy"), WebhookDeliveryResult.Success());

            Assert.False(downDelivered);
            Assert.True(healthyDelivered);
        }

        [Fact]
        public async Task AfterTheBreakDuration_AProbeGoesThrough_AndASuccessClosesTheCircuit() {
            (CircuitBreakerMiddleware middleware, FakeTimeProvider time) = Build();
            for(int i = 0; i < Threshold; i++) {
                await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Transient("502", 502));
            }

            time.Advance(BreakDuration + TimeSpan.FromSeconds(1));
            (bool probe, _) = await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Success());
            (bool afterProbe, _) = await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Success());

            Assert.True(probe);
            Assert.True(afterProbe);
        }

        [Fact]
        public async Task AFailedProbe_OpensTheCircuitAgain() {
            (CircuitBreakerMiddleware middleware, FakeTimeProvider time) = Build();
            for(int i = 0; i < Threshold; i++) {
                await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Transient("502", 502));
            }

            time.Advance(BreakDuration + TimeSpan.FromSeconds(1));
            (bool probe, _) = await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Transient("502", 502));
            (bool afterFailedProbe, _) = await DeliverAsync(middleware, ContextFor("down"), WebhookDeliveryResult.Success());

            Assert.True(probe);
            Assert.False(afterFailedProbe);
        }

        [Fact]
        public async Task LocalThrottling_DoesNotOpenTheCircuitOfAHealthyEndpoint() {
            (CircuitBreakerMiddleware middleware, _) = Build();
            WebhookDeliveryResult throttled = WebhookDeliveryResult.RateLimited("busy", TimeSpan.FromSeconds(1));

            for(int i = 0; i < Threshold * 3; i++) {
                await DeliverAsync(middleware, ContextFor("busy"), throttled);
            }

            (bool delivered, _) = await DeliverAsync(middleware, ContextFor("busy"), WebhookDeliveryResult.Success());
            Assert.True(delivered);
        }
    }
}
