using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Transports.InMemory;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

public sealed class InMemoryWebhookConsumerTests {
    private static (InMemoryWebhookConsumer Consumer, InMemoryWebhookTransport Transport, FakeWebhookJobHandler Handler) CreateSut(int concurrency = 1) {
        InMemoryWebhookTransportOptions options = new() { Concurrency = concurrency };
        InMemoryWebhookTransport transport = new(options);
        FakeWebhookJobHandler handler = new();

        ServiceCollection services = new();
        services.AddSingleton<IWebhookJobHandler>(handler);
        ServiceProvider provider = services.BuildServiceProvider();

        InMemoryWebhookConsumer consumer = new(
            transport,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<InMemoryWebhookConsumer>.Instance);
        return (consumer, transport, handler);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesHandler_ForEachEnqueuedJob() {
        (InMemoryWebhookConsumer consumer, InMemoryWebhookTransport transport, FakeWebhookJobHandler handler) = CreateSut();
        WebhookDeliveryJob job = new(WebhookTestFactory.CreateEndpointId(), WebhookTestFactory.CreateEvent());

        using CancellationTokenSource cts = new();
        Task run = consumer.StartAsync(cts.Token);
        await transport.EnqueueAsync(job);

        await WaitUntil(() => handler.HandledJobs.Count == 1);

        Assert.Same(job, handler.HandledJobs[0]);

        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesProcessing_AfterHandlerThrows() {
        (InMemoryWebhookConsumer consumer, InMemoryWebhookTransport transport, FakeWebhookJobHandler handler) = CreateSut();
        handler.ThrowOnNextHandle = true;

        WebhookDeliveryJob failingJob = new(WebhookTestFactory.CreateEndpointId("fails"), WebhookTestFactory.CreateEvent());
        WebhookDeliveryJob followingJob = new(WebhookTestFactory.CreateEndpointId("ok"), WebhookTestFactory.CreateEvent());

        using CancellationTokenSource cts = new();
        await consumer.StartAsync(cts.Token);
        await transport.EnqueueAsync(failingJob);
        await transport.EnqueueAsync(followingJob);

        await WaitUntil(() => handler.HandledJobs.Count == 1);

        Assert.Same(followingJob, handler.HandledJobs[0]);

        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Constructor_ThrowsWhenAnyParameterIsNull() {
        ServiceCollection services = new();
        services.AddSingleton<IWebhookJobHandler>(new FakeWebhookJobHandler());
        ServiceProvider provider = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        InMemoryWebhookTransport transport = new();
        IOptions<InMemoryWebhookTransportOptions> options = Microsoft.Extensions.Options.Options.Create(new InMemoryWebhookTransportOptions());

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(null!, scopeFactory, NullLogger<InMemoryWebhookConsumer>.Instance));

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(transport, null!, NullLogger<InMemoryWebhookConsumer>.Instance));

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(transport, scopeFactory, null!));

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(null!, scopeFactory, options, NullLogger<InMemoryWebhookConsumer>.Instance));

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(transport, null!, options, NullLogger<InMemoryWebhookConsumer>.Instance));

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(transport, scopeFactory, null!, NullLogger<InMemoryWebhookConsumer>.Instance));

        Assert.ThrowsAny<ArgumentNullException>(() =>
            new InMemoryWebhookConsumer(transport, scopeFactory, options, null!));
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 2000) {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while(!condition() && DateTime.UtcNow < deadline) {
            await Task.Delay(10);
        }
        Assert.True(condition(), "Condition was not met within the timeout.");
    }
}