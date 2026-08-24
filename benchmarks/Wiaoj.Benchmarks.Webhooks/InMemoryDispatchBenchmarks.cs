using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Transports.InMemory;
using Wolverine;

namespace Wiaoj.Webhooks.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 15, warmupCount: 5)]
public class InMemoryDispatchBenchmarks {

    // ── Common Payload ──
    public sealed record BenchmarkOrderEvent(string OrderId, decimal Amount) : IWebhookEvent;

    // ── 1. Tyto / Wiaoj Setup ──
    private ServiceProvider _wiaojProvider = null!;
    private IWebhookDispatcher _wiaojDispatcher = null!;
    private WebhookEndpointId _wiaojEndpointId;

    // ── 2. MassTransit Setup ──
    private ServiceProvider _massTransitProvider = null!;
    private IBusControl _massTransitBus = null!;

    // ── 3. Wolverine Setup ──
    private Microsoft.Extensions.Hosting.IHost _wolverineHost = null!;
    private IMessageBus _wolverineBus = null!;

    [GlobalSetup]
    public async Task Setup() {
        // ── A. Configure Wiaoj ──
        ServiceCollection wiaojServices = new();
        wiaojServices.AddLogging();
        wiaojServices.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        wiaojServices.AddSingleton<ISecretProtector<WebhookSigningContext>>(new FakeSecretProtector<WebhookSigningContext>());

        InMemoryTestEndpointResolver resolver = new();
        this._wiaojEndpointId = new WebhookEndpointId("ep_bench");
        resolver.Register(new WebhookEndpoint(this._wiaojEndpointId, new Uri("http://localhost/benchmark"), new FakeSecretProtector<WebhookSigningContext>().Protect("whsec_key")));
        wiaojServices.AddSingleton<IWebhookEndpointResolver>(resolver);

        wiaojServices.AddWiaojWebhooks(w => {
            w.UseInMemoryTransport(opts => {
                opts.Concurrency = Environment.ProcessorCount;
            });
        });

        this._wiaojProvider = wiaojServices.BuildServiceProvider();
        this._wiaojDispatcher = this._wiaojProvider.GetRequiredService<IWebhookDispatcher>();

        // ── B. Configure MassTransit (Mediator / In-Memory Mode) ──
        ServiceCollection mtServices = new();
        mtServices.AddMassTransit(x => {
            x.AddConsumer<MassTransitOrderConsumer>();
            x.UsingInMemory((ctx, cfg) => {
                cfg.ConfigureEndpoints(ctx);
            });
        });

        this._massTransitProvider = mtServices.BuildServiceProvider();
        this._massTransitBus = this._massTransitProvider.GetRequiredService<IBusControl>();
        await this._massTransitBus.StartAsync();

        // ── C. Configure Wolverine ──
        this._wolverineHost = await Host.CreateDefaultBuilder()
            .UseWolverine(opts => {
                opts.PublishMessage<BenchmarkOrderEvent>().ToLocalQueue("orders");
            })
            .StartAsync();

        this._wolverineBus = this._wolverineHost.Services.GetRequiredService<IMessageBus>();
    }

    [GlobalCleanup]
    public async Task Cleanup() {
        await this._massTransitBus.StopAsync();
        await this._massTransitProvider.DisposeAsync();
        await this._wiaojProvider.DisposeAsync();
        await this._wolverineHost.StopAsync();
        this._wolverineHost.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Wiaoj.Webhooks")]
    public async Task Wiaoj_Dispatch() {
        BenchmarkOrderEvent @event = new("ORD-100", 99.99m);
        await this._wiaojDispatcher.DispatchAsync(this._wiaojEndpointId, @event, CancellationToken.None);
    }

    [Benchmark(Description = "MassTransit")]
    public async Task MassTransit_Publish() {
        BenchmarkOrderEvent @event = new("ORD-100", 99.99m);
        await this._massTransitBus.Publish(@event);
    }

    [Benchmark(Description = "Wolverine")]
    public async Task Wolverine_Publish() {
        BenchmarkOrderEvent @event = new("ORD-100", 99.99m);
        await this._wolverineBus.PublishAsync(@event);
    }

    // ── Test Consumers ──
    private sealed class MassTransitOrderConsumer : IConsumer<BenchmarkOrderEvent> {
        public Task Consume(ConsumeContext<BenchmarkOrderEvent> context) => Task.CompletedTask;
    }

    public sealed class WolverineOrderHandler {
        public void Handle(BenchmarkOrderEvent message) { }
    }

    private sealed class InMemoryTestEndpointResolver : IWebhookEndpointResolver {
        private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = [];
        public void Register(WebhookEndpoint endpoint) => this._endpoints[endpoint.Id] = endpoint;
        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
    }
}