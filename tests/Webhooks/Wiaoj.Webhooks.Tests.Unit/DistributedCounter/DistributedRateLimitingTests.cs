using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.Webhooks.RateLimiting;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.DistributedCounter;

[Trait("Category", "Unit")]
[Trait("Feature", "RateLimiting")]
[Trait("Component", "Middleware")]
public sealed class WebhookRateLimitingMiddlewareTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class SpyRateLimiter : IRateLimiter {
        public string? LastKey { get; private set; }
        public string? LastPolicyName { get; private set; }
        public int LastCost { get; private set; }
        public RateLimitDecision DecisionToReturn { get; set; } = RateLimitDecision.Allowed(5);

        public IRateLimitAlgorithm GetPolicy(string policyName) {
            return new StubAlgorithm(this.DecisionToReturn);
        }

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.LastKey = key;
            this.LastCost = cost;
            this.LastPolicyName = null;
            return ValueTask.FromResult(this.DecisionToReturn);
        }

        public ValueTask<RateLimitDecision> TryAcquireAsync(string policyName, string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.LastKey = key;
            this.LastCost = cost;
            this.LastPolicyName = policyName;
            return ValueTask.FromResult(this.DecisionToReturn);
        }

        private sealed class StubAlgorithm(RateLimitDecision decision) : IRateLimitAlgorithm {
            public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
                return ValueTask.FromResult(decision);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CORE EXECUTION & FLOW TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheCoreExecutionFlow {
        [Fact]
        public async Task InvokeAsync_WhenRateLimiterAllows_InvokesNextDelegateAndLeavesResultUntouched() {
            SpyRateLimiter limiter = new() {
                DecisionToReturn = RateLimitDecision.Allowed(remaining: 10)
            };
            WebhookRateLimitingOptions options = new();
            WebhookRateLimitingMiddleware sut = new(limiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            await sut.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            Assert.True(nextInvoked);
            Assert.False(context.TryGetResult(out _));
        }

        [Fact]
        public async Task InvokeAsync_WhenRateLimiterDenies_SetsRateLimitedResultAndShortCircuitsNext() {
            TimeSpan retryAfter = TimeSpan.FromSeconds(15);
            SpyRateLimiter limiter = new() {
                DecisionToReturn = RateLimitDecision.Denied(retryAfter, remaining: 0)
            };
            WebhookRateLimitingOptions options = new();
            WebhookRateLimitingMiddleware sut = new(limiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            await sut.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));

            WebhookDeliveryResult.TransientFailure transient = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
            Assert.False(transient.IsSuccess);
            Assert.Equal(429, transient.StatusCode);
            Assert.Equal(retryAfter, transient.RetryAfter);
            Assert.Equal(TransientFailureReason.RateLimitThrottled, transient.Reason);
            Assert.Contains("Rate limit exceeded", transient.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvokeAsync_PassesCorrectKeyAndCostToRateLimiter() {
            SpyRateLimiter limiter = new();
            WebhookRateLimitingOptions options = new() {
                KeySelector = ctx => $"custom:key:{ctx.Endpoint.Id.Value}",
                CostResolver = _ => 3
            };
            WebhookRateLimitingMiddleware sut = new(limiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await sut.InvokeAsync(context, static (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken);

            Assert.Equal($"custom:key:{context.Endpoint.Id.Value}", limiter.LastKey);
            Assert.Equal(3, limiter.LastCost);
            Assert.Null(limiter.LastPolicyName);
        }

        [Fact]
        public async Task InvokeAsync_WithNamedPolicyConfigured_PassesPolicyNameToRateLimiter() {
            SpyRateLimiter limiter = new();
            WebhookRateLimitingOptions options = new() {
                PolicyName = "Tier1_Webhooks",
                KeySelector = ctx => ctx.Endpoint.Id.Value
            };
            WebhookRateLimitingMiddleware sut = new(limiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await sut.InvokeAsync(context, static (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken);

            Assert.Equal(context.Endpoint.Id.Value, limiter.LastKey);
            Assert.Equal("Tier1_Webhooks", limiter.LastPolicyName);
        }

        [Fact]
        public async Task InvokeAsync_WhenDeniedWithoutExplicitRetryAfter_FallsBackToOneSecond() {
            SpyRateLimiter limiter = new() {
                DecisionToReturn = RateLimitDecision.Denied(TimeSpan.Zero, remaining: 0)
            };
            WebhookRateLimitingOptions options = new();
            WebhookRateLimitingMiddleware sut = new(limiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await sut.InvokeAsync(context, static (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken);

            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.TransientFailure transient = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
            Assert.Equal(TimeSpan.FromSeconds(1), transient.RetryAfter);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. OPTIONS & KEY SELECTOR
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheOptionsAndKeySelector {
        [Fact]
        public void DefaultKeySelector_FormatsKeyWithEndpointId() {
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            string key = WebhookRateLimitingOptions.DefaultKeySelector(context);

            Assert.Equal($"wh:ratelimit:{context.Endpoint.Id.Value}", key);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. INTEGRATION WITH REAL RATE LIMITER & TIME SIMULATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheRealRateLimiterIntegration {
        [Fact]
        public async Task InvokeAsync_WithRealRateLimiter_AllowsUpToLimitThenReEnqueuesUntilWindowResets() {
            // Arrange: 2 requests allowed per 5 seconds window using DefaultRateLimiter in DI
            FakeTimeProvider time = new(Epoch);

            ServiceCollection services = new();
            services.AddSingleton<TimeProvider>(time);
            services.AddDistributedCounter(dc => dc.UseInMemory());
            services.AddWiaojRateLimiting(rl => {
                rl.UseDefaultPolicy(p => p.UseFixedWindow(limit: 2, window: TimeSpan.FromSeconds(5)));
            });

            using ServiceProvider sp = services.BuildServiceProvider();
            IRateLimiter realLimiter = sp.GetRequiredService<IRateLimiter>();

            WebhookRateLimitingOptions options = new();
            WebhookRateLimitingMiddleware sut = new(realLimiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            int successfulDeliveries = 0;
            WebhookDelegate next = (ctx, ct) => {
                successfulDeliveries++;
                return Task.CompletedTask;
            };

            // 1st delivery (Allowed)
            await sut.InvokeAsync(context, next, TestContext.Current.CancellationToken);
            Assert.Equal(1, successfulDeliveries);

            // 2nd delivery (Allowed)
            await sut.InvokeAsync(context, next, TestContext.Current.CancellationToken);
            Assert.Equal(2, successfulDeliveries);

            // 3rd delivery (Blocked -> Re-enqueued with 429)
            WebhookDeliveryContext blockedContext = WebhookTestFactory.CreateContext();
            await sut.InvokeAsync(blockedContext, next, TestContext.Current.CancellationToken);
            Assert.Equal(2, successfulDeliveries);
            Assert.True(blockedContext.TryGetResult(out WebhookDeliveryResult? blockedResult));
            Assert.Equal(429, ((WebhookDeliveryResult.TransientFailure)blockedResult!).StatusCode);
            Assert.Equal(TransientFailureReason.RateLimitThrottled, ((WebhookDeliveryResult.TransientFailure)blockedResult).Reason);

            // Advance time by 5.1 seconds (Window resets)
            time.Advance(TimeSpan.FromSeconds(5.1));

            // 4th delivery (Allowed again)
            WebhookDeliveryContext resetContext = WebhookTestFactory.CreateContext();
            await sut.InvokeAsync(resetContext, next, TestContext.Current.CancellationToken);
            Assert.Equal(3, successfulDeliveries);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. DI REGISTRATION TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheDiRegistration {
        [Fact]
        public void UseRateLimiting_RegistersMiddlewareAndOptionsInContainer() {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IWebhookTransport, FakeWebhookTransport>();
            services.AddDistributedCounter(dc => dc.UseInMemory());
            services.AddWiaojRateLimiting(rl => {
                rl.UseDefaultPolicy(p => p.UseFixedWindow(10, TimeSpan.FromMinutes(1)));
            });

            services.AddWiaojWebhooks(webhookBuilder => {
                webhookBuilder.UseRateLimiting(options => {
                    options.CostResolver = _ => 2;
                });
            });

            using ServiceProvider sp = services.BuildServiceProvider();

            WebhookRateLimitingMiddleware middleware = sp.GetRequiredService<WebhookRateLimitingMiddleware>();
            WebhookRateLimitingOptions options = sp.GetRequiredService<WebhookRateLimitingOptions>();

            Assert.NotNull(middleware);
            Assert.NotNull(options);
            Assert.Equal(2, options.CostResolver(WebhookTestFactory.CreateContext()));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. CONSTRUCTOR GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructorGuards {
        [Fact]
        public void Constructor_WithNullRateLimiter_Throws() {
            Assert.ThrowsAny<ArgumentNullException>(
                () => new WebhookRateLimitingMiddleware(null!, new WebhookRateLimitingOptions(), NullLogger<WebhookRateLimitingMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_WithNullOptions_Throws() {
            Assert.ThrowsAny<ArgumentNullException>(
                () => new WebhookRateLimitingMiddleware(new SpyRateLimiter(), null!, NullLogger<WebhookRateLimitingMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_WithNullLogger_Throws() {
            Assert.ThrowsAny<ArgumentNullException>(
                () => new WebhookRateLimitingMiddleware(new SpyRateLimiter(), new WebhookRateLimitingOptions(), null!));
        }
    }
}