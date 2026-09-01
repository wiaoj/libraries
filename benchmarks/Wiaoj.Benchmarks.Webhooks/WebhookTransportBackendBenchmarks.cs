using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Tyto;
using Tyto.DependencyInjection;
using Wiaoj.Benchmarks.Webhooks.Transports;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks;
using Wolverine;
using IHost = Microsoft.Extensions.Hosting.IHost;

namespace Wiaoj.Benchmarks.Webhooks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 15, warmupCount: 5)]
public class WebhookTransportBackendBenchmarks {
    [Params(10_000, 100_000, 1_000_000)]
    public int OperationsPerInvoke { get; set; }

    private static readonly int Concurrency = Environment.ProcessorCount;

    [WebhookEvent("order.created")]
    public sealed record BenchmarkOrderEvent(string OrderId, decimal Amount) : IWebhookEvent;

    private readonly BenchmarkOrderEvent _sampleEvent = new("ORD-BENCH-1", 150m);

    // ── 1. Tyto Setup ──
    private IHost _tytoHost = null!;
    private IWebhookDispatcher _tytoDispatcher = null!;

    // ── 2. MassTransit Setup ──
    private ServiceProvider _massTransitProvider = null!;
    private IWebhookDispatcher _massTransitDispatcher = null!;
    private IBusControl _massTransitBus = null!;

    // ── 3. Wolverine Setup ──
    private IHost _wolverineHost = null!;
    private IWebhookDispatcher _wolverineDispatcher = null!;

    private WebhookEndpointId _endpointId;

    [GlobalSetup]
    public async Task Setup() {
        this._endpointId = new WebhookEndpointId("ep_benchmark");
        FakeSecretProtector<WebhookSigningContext> protector = new();

        InMemoryTestEndpointResolver resolver = new();
        resolver.Register(new WebhookEndpoint(
            this._endpointId,
            new Uri("http://localhost/bench"),
            protector.Protect("whsec_bench_secret")));

        // ═════════════════════════════════════════════════════════════════════
        // A. Setup: Tyto
        // ═════════════════════════════════════════════════════════════════════
        HostApplicationBuilder tytoBuilder = Host.CreateApplicationBuilder();
        tytoBuilder.Services.AddLogging(l => l.ClearProviders().SetMinimumLevel(LogLevel.None));
        tytoBuilder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
        tytoBuilder.Services.AddSingleton<IWebhookEndpointResolver>(resolver);
        tytoBuilder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        tytoBuilder.Services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

        tytoBuilder.AddTyto(tyto => {
            tyto.MessageDefinitions(d => d.Add<TytoWebhookJobEnvelope>("webhook.delivery.job", 1));

            tyto.Publishing(x => {
                x.UseAsyncDispatch(100_000); 
            });

            tyto.Transports(t => {
                t.AddInMemory("memory", opt => {
                    opt.DefaultConcurrencyLimit = Concurrency;
                    opt.FullMode = BoundedChannelFullMode.Wait;
                    opt.ChannelCapacity = 100_000;
                    opt.Bind("ex.webhook.jobs", "q.webhook.jobs");
                });
            });

            tyto.Endpoints(ep => {
                ep.Add("WEBHOOK-DISPATCH-EP", e => {
                    e.ListenOn("memory", "q.webhook.jobs");
                    e.Routing.Publish<TytoWebhookJobEnvelope>().To("memory", "ex.webhook.jobs");
                    e.AddHandler<TytoWebhookJobHandler>();
                });
            });
        });

        tytoBuilder.Services.AddWiaojWebhooks(w => {
            w.Services.AddSingleton<IWebhookTransport, TytoWebhookTransport>(); 
            w.RegisterEvent<BenchmarkOrderEvent>("order.created");
        });

        this._tytoHost = tytoBuilder.Build();
        await this._tytoHost.StartAsync();
        this._tytoDispatcher = this._tytoHost.Services.GetRequiredService<IWebhookDispatcher>();

        // ═════════════════════════════════════════════════════════════════════
        // B. Setup: MassTransit
        // ═════════════════════════════════════════════════════════════════════
        ServiceCollection mtServices = new();
        mtServices.AddLogging(l => l.ClearProviders().SetMinimumLevel(LogLevel.None));
        mtServices.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
        mtServices.AddSingleton<IWebhookEndpointResolver>(resolver);
        mtServices.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        mtServices.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

        mtServices.AddMassTransit(x => {
            x.AddConsumer<MassTransitWebhookJobConsumer>();
            x.UsingInMemory((ctx, cfg) => {
                cfg.ConcurrentMessageLimit = Concurrency;
                cfg.ConfigureEndpoints(ctx);
            });
        });

        mtServices.AddWiaojWebhooks(w => {
            w.Services.AddSingleton<IWebhookTransport, MassTransitWebhookTransport>();
            w.RegisterEvent<BenchmarkOrderEvent>("order.created");
        });

        this._massTransitProvider = mtServices.BuildServiceProvider();
        this._massTransitBus = this._massTransitProvider.GetRequiredService<IBusControl>();
        await this._massTransitBus.StartAsync();
        this._massTransitDispatcher = this._massTransitProvider.GetRequiredService<IWebhookDispatcher>();

        // ═════════════════════════════════════════════════════════════════════
        // C. Setup: Wolverine
        // ═════════════════════════════════════════════════════════════════════
        this._wolverineHost = await Host.CreateDefaultBuilder()
            .ConfigureServices(services => {
                services.AddLogging(l => l.ClearProviders().SetMinimumLevel(LogLevel.None));
                services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
                services.AddSingleton<IWebhookEndpointResolver>(resolver);
                services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
                services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

                services.AddWiaojWebhooks(w => {
                    w.Services.AddSingleton<IWebhookTransport, WolverineWebhookTransport>();
                    w.RegisterEvent<BenchmarkOrderEvent>("order.created");
                });
            })
            .UseWolverine(opts => {
                opts.PublishMessage<WebhookDeliveryJob>()
                    .ToLocalQueue("webhook_jobs")
                    .MaximumParallelMessages(Concurrency);
            })
            .StartAsync();

        this._wolverineDispatcher = this._wolverineHost.Services.GetRequiredService<IWebhookDispatcher>();
    }

    [GlobalCleanup]
    public async Task Cleanup() {
        await this._massTransitBus.StopAsync();
        await this._massTransitProvider.DisposeAsync();
        await this._tytoHost.StopAsync();
        this._tytoHost.Dispose();
        await this._wolverineHost.StopAsync();
        this._wolverineHost.Dispose();
    }

    // ────────────────────────────────────────────────────────────────────────
    // BENCHMARKS (10.000 mesaj gerçekten TÜKETİLENE kadar bekler)
    // ────────────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Webhooks + Tyto")]
    public async Task Webhooks_With_Tyto() {
        BenchmarkCompletionTracker.Reset(OperationsPerInvoke);

        for(int i = 0; i < OperationsPerInvoke; i++) {
            await this._tytoDispatcher.DispatchAsync(this._endpointId, this._sampleEvent, CancellationToken.None);
        }

        await BenchmarkCompletionTracker.WaitForCompletionAsync();
    }

    [Benchmark(Description = "Webhooks + MassTransit")]
    public async Task Webhooks_With_MassTransit() {
        BenchmarkCompletionTracker.Reset(OperationsPerInvoke);

        for(int i = 0; i < OperationsPerInvoke; i++) {
            await this._massTransitDispatcher.DispatchAsync(this._endpointId, this._sampleEvent, CancellationToken.None);
        }

        await BenchmarkCompletionTracker.WaitForCompletionAsync();
    }

    [Benchmark(Description = "Webhooks + Wolverine")]
    public async Task Webhooks_With_Wolverine() {
        BenchmarkCompletionTracker.Reset(OperationsPerInvoke);

        for(int i = 0; i < OperationsPerInvoke; i++) {
            await this._wolverineDispatcher.DispatchAsync(this._endpointId, this._sampleEvent, CancellationToken.None);
        }

        await BenchmarkCompletionTracker.WaitForCompletionAsync();
    }

    // ── No-Op Job Handler (Sadece transport overhead'ini ölçmek için) ──
    private sealed class NoOpWebhookJobHandler : IWebhookJobHandler {
        private static readonly WebhookDeliveryAttempt DummyAttempt = new(
            new WebhookEndpointId("ep_benchmark"),
            1,
            UnixTimestamp.Now, // veya default(UnixTimestamp)
            TimeSpan.Zero,
            WebhookDeliveryResult.Success() // WebhookDeliveryResult.Success veya default
        );

        private static readonly Task<WebhookDeliveryAttempt> CachedResult = Task.FromResult(DummyAttempt);

        public Task<WebhookDeliveryAttempt> HandleAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
            return CachedResult;
        }
    }

    private sealed class InMemoryTestEndpointResolver : IWebhookEndpointResolver {
        private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = [];
        public void Register(WebhookEndpoint endpoint) => this._endpoints[endpoint.Id] = endpoint;
        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
    }
}