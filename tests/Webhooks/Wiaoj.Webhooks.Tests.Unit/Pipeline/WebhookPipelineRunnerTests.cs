using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Pipeline;

public sealed class WebhookPipelineRunnerTests {
    private static WebhookPipelineRunner CreateRunner(IReadOnlyList<IWebhookMiddleware> middleware, IWebhookDeliverer deliverer) {
        FakeTimeProvider timeProvider = new();
        return new(middleware, deliverer, timeProvider, NullLogger<WebhookPipelineRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_InvokesMiddlewareInRegisteredOrder_ThenDeliverer() {
        List<string> log = [];
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner(
            [new RecordingWebhookMiddleware("m1", log), new RecordingWebhookMiddleware("m2", log)],
            deliverer);

        await runner.RunAsync(WebhookTestFactory.CreateContext());

        Assert.Equal(["m1:before", "m2:before", "m2:after", "m1:after"], log);
    }

    [Fact]
    public async Task RunAsync_CallsDeliverer_WhenNoMiddlewareRegistered() {
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner([], deliverer);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        await runner.RunAsync(context);

        Assert.Single(deliverer.ReceivedContexts);
        Assert.Same(context, deliverer.ReceivedContexts[0]);
    }

    [Fact]
    public async Task RunAsync_DoesNotCallDeliverer_WhenMiddlewareShortCircuits() {
        List<string> log = [];
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner(
            [new ShortCircuitingWebhookMiddleware(log), new RecordingWebhookMiddleware("never", log)],
            deliverer);

        await runner.RunAsync(WebhookTestFactory.CreateContext());

        Assert.Equal(["short-circuit"], log);
        Assert.Empty(deliverer.ReceivedContexts);
    }

    [Fact]
    public async Task RunAsync_ReturnsAttempt_WithNumberOneMoreThanExistingHistory() {
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner([], deliverer);
        List<WebhookDeliveryAttempt> history = [WebhookTestFactory.CreateAttempt(attemptNumber: 1)];
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(attemptHistory: history);

        WebhookDeliveryAttempt attempt = await runner.RunAsync(context);

        Assert.Equal(2, attempt.AttemptNumber);
    }

    [Fact]
    public async Task RunAsync_ReturnsAttemptNumberOne_WhenHistoryIsEmpty() {
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner([], deliverer);

        WebhookDeliveryAttempt attempt = await runner.RunAsync(WebhookTestFactory.CreateContext());

        Assert.Equal(1, attempt.AttemptNumber);
    }

    [Fact]
    public async Task RunAsync_ReturnsAttemptCarryingDelivererResult() {
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("target unreachable", 503);
        FakeWebhookDeliverer deliverer = new(failure);
        WebhookPipelineRunner runner = CreateRunner([], deliverer);

        WebhookDeliveryAttempt attempt = await runner.RunAsync(WebhookTestFactory.CreateContext());

        Assert.False(attempt.IsSuccess);
        Assert.Same(failure, attempt.Result);
    }

    [Fact]
    public async Task RunAsync_MeasuresNonNegativeDuration() {
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner([], deliverer);

        WebhookDeliveryAttempt attempt = await runner.RunAsync(WebhookTestFactory.CreateContext());

        Assert.True(attempt.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_UsesEndpointIdFromContext() {
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(id: WebhookTestFactory.CreateEndpointId("acme-42"));
        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());
        WebhookPipelineRunner runner = CreateRunner([], deliverer);

        WebhookDeliveryAttempt attempt = await runner.RunAsync(WebhookTestFactory.CreateContext(endpoint: endpoint));

        Assert.Equal(endpoint.Id, attempt.EndpointId);
    }

    [Fact]
    public void Constructor_ThrowsWhenMiddlewareListIsNull() {
        Assert.ThrowsAny<ArgumentException>(() =>
            new WebhookPipelineRunner(null!, new FakeWebhookDeliverer(), TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsWhenDelivererIsNull() {
        Assert.ThrowsAny<ArgumentException>(() =>
            new WebhookPipelineRunner([], null!, TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsWhenTimeProviderIsNull() {
        Assert.ThrowsAny<ArgumentException>(() =>
            new WebhookPipelineRunner([], new FakeWebhookDeliverer(), null!, NullLogger<WebhookPipelineRunner>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull() {
        Assert.ThrowsAny<ArgumentException>(() =>
            new WebhookPipelineRunner([], new FakeWebhookDeliverer(), TimeProvider.System, null!));
    }
}