using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

public sealed class InMemoryTransportConcurrencyTests {
    [Fact]
    public async Task Consumer_ProcessesHighVolumeConcurrently_WithMultipleWorkers() {
        InMemoryWebhookTransportOptions options = new() { Concurrency = 8 };
        InMemoryWebhookTransport transport = new(options);
        FakeWebhookJobHandler handler = new();

        ServiceCollection services = new();
        services.AddSingleton<IWebhookJobHandler>(handler);
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        InMemoryWebhookConsumer consumer = new(
            transport,
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<InMemoryWebhookConsumer>.Instance);

        using CancellationTokenSource cts = new();
        Task consumerTask = consumer.StartAsync(cts.Token);

        const int jobCount = 100;
        for(int i = 0; i < jobCount; i++) {
            WebhookDeliveryJob job = new(WebhookTestFactory.CreateEndpointId($"client-{i}"), WebhookTestFactory.CreateEvent());
            await transport.EnqueueAsync(job);
        }

        // Wait until all 100 jobs are handled by the 8 concurrent workers
        while(handler.HandledJobs.Count < jobCount) {
            await Task.Delay(10);
        }

        Assert.Equal(jobCount, handler.HandledJobs.Count);

        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);
    }
}
