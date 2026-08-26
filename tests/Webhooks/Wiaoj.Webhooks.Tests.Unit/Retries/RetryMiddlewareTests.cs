using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Retries;

public sealed class RetryMiddlewareTests {
    [Fact]
    public async Task InvokeAsync_ReEnqueuesJob_WhenPolicyDeterminesRetry() {
        FakeWebhookTransport transport = new();
        ExponentialBackoffOptions options = new() {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(5),
            Jitter = null
        };
        ExponentialBackoffPolicy policy = new(options);
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        WebhookDeliveryResult failureResult = WebhookTestFactory.CreateFailureResult("Service Unavailable", 503);

        WebhookDelegate next = (ctx, ct) => {
            ctx.Items[WebhookDeliveryContextItemKeys.Result] = failureResult;
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        Assert.Single(transport.EnqueuedJobs);
        (WebhookDeliveryJob job, TimeSpan? delay) = transport.EnqueuedJobs[0];
        Assert.Equal(context.Endpoint.Id, job.EndpointId);
        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotReEnqueue_WhenDeliveryIsSuccessful() {
        FakeWebhookTransport transport = new();
        ExponentialBackoffPolicy policy = new();
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        WebhookDeliveryResult successResult = WebhookTestFactory.CreateSuccessResult(200);

        WebhookDelegate next = (ctx, ct) => {
            ctx.Items[WebhookDeliveryContextItemKeys.Result] = successResult;
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        Assert.Empty(transport.EnqueuedJobs);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotReEnqueue_WhenPolicyRejects_MaxAttemptsReached() {
        FakeWebhookTransport transport = new();
        ExponentialBackoffOptions options = new() { MaxAttempts = 2 };
        ExponentialBackoffPolicy policy = new(options);
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        // History already has attempt 1, so this attempt is #2 (max attempts)
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, WebhookTestFactory.CreateFailureResult("500", 500))
        ]);
        WebhookDeliveryResult failureResult = WebhookTestFactory.CreateFailureResult("500", 500);

        WebhookDelegate next = (ctx, ct) => {
            ctx.Items[WebhookDeliveryContextItemKeys.Result] = failureResult;
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        Assert.Empty(transport.EnqueuedJobs);
    }

    [Fact]
    public void Constructor_Throws_WhenArgumentsAreNull() {
        FakeWebhookTransport transport = new();
        ExponentialBackoffPolicy policy = new();

        Action act1 = () => _ = new RetryMiddleware(null!, transport, NullLogger<RetryMiddleware>.Instance);
        Action act2 = () => _ = new RetryMiddleware(policy, null!, NullLogger<RetryMiddleware>.Instance);
        Action act3 = () => _ = new RetryMiddleware(policy, transport, null!);

        Assert.ThrowsAny<ArgumentException>(act1);
        Assert.ThrowsAny<ArgumentException>(act2);
        Assert.ThrowsAny<ArgumentException>(act3);
    }
}
