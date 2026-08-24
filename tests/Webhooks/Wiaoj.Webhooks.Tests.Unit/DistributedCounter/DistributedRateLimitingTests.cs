using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.Testing;
using Wiaoj.Webhooks.RateLimiting;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.RateLimiting;

public sealed class WebhookRateLimitingMiddlewareTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Spy / Fake algorithm for controlled testing of webhook rate limiting behavior
    private sealed class SpyRateLimitAlgorithm : IRateLimitAlgorithm {
        public string? LastKey { get; private set; }
        public int LastCost { get; private set; }
        public RateLimitDecision DecisionToReturn { get; set; } = RateLimitDecision.Allowed(5);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.LastKey = key;
            this.LastCost = cost;
            return ValueTask.FromResult(this.DecisionToReturn);
        }
    }

    // ---------------------------------------------------------------------
    // 1. Core Execution & Flow Tests
    // ---------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_WhenAlgorithmAllows_InvokesNextDelegateAndLeavesResultUntouched() {
        // Arrange
        SpyRateLimitAlgorithm algorithm = new() {
            DecisionToReturn = RateLimitDecision.Allowed(remaining: 10)
        };
        WebhookRateLimitingOptions options = new();
        WebhookRateLimitingMiddleware sut = new(algorithm, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        bool nextInvoked = false;
        WebhookDelegate next = (_, _) => {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        // Act
        await sut.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(nextInvoked);
        Assert.False(context.TryGetResult(out _)); // No failure result set, delivery continues!
    }

    [Fact]
    public async Task InvokeAsync_WhenAlgorithmDenies_SetsTransient429ResultWithRetryAfterAndShortCircuitsNext() {
        // Arrange
        TimeSpan retryAfter = TimeSpan.FromSeconds(15);
        SpyRateLimitAlgorithm algorithm = new() {
            DecisionToReturn = RateLimitDecision.Denied(retryAfter, remaining: 0)
        };
        WebhookRateLimitingOptions options = new();
        WebhookRateLimitingMiddleware sut = new(algorithm, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        bool nextInvoked = false;
        WebhookDelegate next = (_, _) => {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        // Act
        await sut.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        // Assert: Next delegate must NOT be called
        Assert.False(nextInvoked);

        // Assert: Result must be a Transient failure (Re-enqueued for retry) with 429 and RetryAfter
        Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
        WebhookDeliveryResult.TransientFailure transient = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
        Assert.False(transient.IsSuccess);
        Assert.Equal(429, transient.StatusCode);
        Assert.Equal(retryAfter, transient.RetryAfter);
        Assert.Contains("Rate limit exceeded", transient.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_PassesCorrectKeyAndCostToAlgorithm() {
        // Arrange
        SpyRateLimitAlgorithm algorithm = new();
        WebhookRateLimitingOptions options = new() {
            KeySelector = ctx => $"custom:key:{ctx.Endpoint.Id.Value}",
            CostResolver = _ => 3
        };
        WebhookRateLimitingMiddleware sut = new(algorithm, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        // Act
        await sut.InvokeAsync(context, (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal($"custom:key:{context.Endpoint.Id.Value}", algorithm.LastKey);
        Assert.Equal(3, algorithm.LastCost);
    }

    [Fact]
    public async Task InvokeAsync_WhenDeniedWithoutExplicitRetryAfter_FallsBackToDefaultOneSecond() {
        // Arrange
        SpyRateLimitAlgorithm algorithm = new() {
            // RateLimitDecision denied without positive retryAfter
            DecisionToReturn = RateLimitDecision.Denied(TimeSpan.Zero, remaining: 0)
        };
        WebhookRateLimitingOptions options = new();
        WebhookRateLimitingMiddleware sut = new(algorithm, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        // Act
        await sut.InvokeAsync(context, (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
        WebhookDeliveryResult.TransientFailure transient = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
        Assert.Equal(TimeSpan.FromSeconds(1), transient.RetryAfter); // Safe fallback
    }

    // ---------------------------------------------------------------------
    // 2. Options & Default Format Tests
    // ---------------------------------------------------------------------

    [Fact]
    public void DefaultKeySelector_FormatsKeyWithEndpointId() {
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        string key = WebhookRateLimitingOptions.DefaultKeySelector(context);

        Assert.Equal($"wh:ratelimit:{context.Endpoint.Id.Value}", key);
    }

    // ---------------------------------------------------------------------
    // 3. Integration with FakeRateLimitAlgorithm & Time Simulation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_WithFakeRateLimiter_AllowsUpToLimitThenReEnqueuesUntilWindowResets() {
        // Arrange: 2 requests allowed per 5 seconds window
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm realFakeLimiter = new(limit: 2, window: TimeSpan.FromSeconds(5), time);
        WebhookRateLimitingOptions options = new();
        WebhookRateLimitingMiddleware sut = new(realFakeLimiter, options, NullLogger<WebhookRateLimitingMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        int successfulDeliveries = 0;
        WebhookDelegate next = (_, _) => {
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
        Assert.Equal(2, successfulDeliveries); // Not called
        Assert.True(blockedContext.TryGetResult(out WebhookDeliveryResult? blockedResult));
        Assert.Equal(429, ((WebhookDeliveryResult.TransientFailure)blockedResult!).StatusCode);

        // Advance time by 5.1 seconds (Window resets)
        time.Advance(TimeSpan.FromSeconds(5.1));

        // 4th delivery (Allowed again)
        WebhookDeliveryContext resetContext = WebhookTestFactory.CreateContext();
        await sut.InvokeAsync(resetContext, next, TestContext.Current.CancellationToken);
        Assert.Equal(3, successfulDeliveries);
    }

    // ---------------------------------------------------------------------
    // 4. DI Registration Tests
    // ---------------------------------------------------------------------

    [Fact]
    public void UseRateLimiting_RegistersMiddlewareAndOptionsInContainer() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IWebhookTransport, FakeWebhookTransport>();
        services.AddSingleton<IRateLimitAlgorithm>(new FakeRateLimitAlgorithm(10, TimeSpan.FromMinutes(1)));

        services.AddWiaojWebhooks(webhookBuilder => {
            webhookBuilder.UseRateLimiting(options => {
                options.CostResolver = _ => 2;
            });
        });

        ServiceProvider sp = services.BuildServiceProvider();

        WebhookRateLimitingMiddleware middleware = sp.GetRequiredService<WebhookRateLimitingMiddleware>();
        WebhookRateLimitingOptions options = sp.GetRequiredService<WebhookRateLimitingOptions>();

        Assert.NotNull(middleware);
        Assert.NotNull(options);
        Assert.Equal(2, options.CostResolver(WebhookTestFactory.CreateContext()));
    }

    // ---------------------------------------------------------------------
    // 5. Constructor Argument Validation
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullAlgorithm_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(
            () => new WebhookRateLimitingMiddleware(null!, new WebhookRateLimitingOptions(), NullLogger<WebhookRateLimitingMiddleware>.Instance));
    }

    [Fact]
    public void Constructor_WithNullOptions_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(
            () => new WebhookRateLimitingMiddleware(new SpyRateLimitAlgorithm(), null!, NullLogger<WebhookRateLimitingMiddleware>.Instance));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(
            () => new WebhookRateLimitingMiddleware(new SpyRateLimitAlgorithm(), new WebhookRateLimitingOptions(), null!));
    }
}