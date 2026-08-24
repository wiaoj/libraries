using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tyto;
using Tyto.DependencyInjection;
using Wiaoj.Benchmarks.Webhooks.Transports;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks;
using Wolverine;

namespace Wiaoj.Benchmarks.Webhooks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 20, warmupCount: 5)]
public class WebhookTransportBackendBenchmarks {

    private const int OperationsPerInvoke = 10_000;

    public sealed record BenchmarkOrderEvent(string OrderId, decimal Amount) : IWebhookEvent;

    // ── 1. Wiaoj + Tyto Engine ──
    private Microsoft.Extensions.Hosting.IHost _tytoHost = null!;
    private IWebhookDispatcher _tytoDispatcher = null!;

    // ── 2. Wiaoj + MassTransit Engine ──
    private ServiceProvider _massTransitProvider = null!;
    private IWebhookDispatcher _massTransitDispatcher = null!;
    private IBusControl _massTransitBus = null!;

    // ── 3. Wiaoj + Wolverine Engine ──
    private Microsoft.Extensions.Hosting.IHost _wolverineHost = null!;
    private IWebhookDispatcher _wolverineDispatcher = null!;

    private WebhookEndpointId _endpointId;

    [GlobalSetup]
    public async Task Setup() {
        this._endpointId = new WebhookEndpointId("ep_benchmark");
        FakeSecretProtector<WebhookSigningContext> protector = new();

        InMemoryTestEndpointResolver resolver = new();
        resolver.Register(new WebhookEndpoint(this._endpointId, new Uri("http://localhost/bench"), protector.Protect("whsec_bench_secret")));

        // ═════════════════════════════════════════════════════════════════════
        // A. Setup: Wiaoj Webhooks + TYTO Transport
        // ═════════════════════════════════════════════════════════════════════
        HostApplicationBuilder tytoHostBuilder = Host.CreateApplicationBuilder();
        tytoHostBuilder.Services.AddLogging(); 
        tytoHostBuilder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
        tytoHostBuilder.Services.AddSingleton<IWebhookEndpointResolver>(resolver);

        tytoHostBuilder.Services.AddWiaojSerializer(opts => {
            opts.UseSystemTextJson<WebhookSerializerKey>(); 
            opts.UseSystemTextJson<TytoJsonSerializerKey>(json => {
                json.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });
        });

        tytoHostBuilder.AddTyto(tyto => {
            // 1. Mesaj Tanımı
            tyto.MessageDefinitions(define => {
                define.Add<TytoWebhookJobEnvelope>("webhook.delivery.job", 1);
            });

            // 2. In-Memory Transport & Binding
            tyto.Transports(transports => {
                transports.AddInMemory("memory", options => {
                    options.Bind("ex.webhook.jobs", "q.webhook.jobs");
                });
            });

            // 3. Endpoint & Routing
            tyto.Endpoints(endpoints => {
                endpoints.Add("WEBHOOK-DISPATCH-EP", ep => {
                    ep.ListenOn("memory", "q.webhook.jobs");
                    ep.Routing.Publish<TytoWebhookJobEnvelope>().To("memory", "ex.webhook.jobs");
                    ep.AddHandler<TytoWebhookJobHandler>();
                });
            });
        });

        tytoHostBuilder.Services.AddWiaojWebhooks(w => {
            w.Services.AddSingleton<IWebhookTransport, TytoWebhookTransport>();
        });

        this._tytoHost = tytoHostBuilder.Build();
        await this._tytoHost.StartAsync();

        this._tytoDispatcher = this._tytoHost.Services.GetRequiredService<IWebhookDispatcher>();

        // ═════════════════════════════════════════════════════════════════════
        // B. Setup: Wiaoj Webhooks + MASSTRANSIT Transport
        // ═════════════════════════════════════════════════════════════════════
        ServiceCollection mtServices = new();
        mtServices.AddLogging();
        mtServices.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        mtServices.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
        mtServices.AddSingleton<IWebhookEndpointResolver>(resolver);

        mtServices.AddMassTransit(x => {
            x.AddConsumer<MassTransitWebhookJobConsumer>();
            x.UsingInMemory((ctx, cfg) => {
                cfg.ConfigureEndpoints(ctx);
            });
        });

        mtServices.AddWiaojWebhooks(w => {
            w.Services.AddSingleton<IWebhookTransport, MassTransitWebhookTransport>();
        });

        this._massTransitProvider = mtServices.BuildServiceProvider();
        this._massTransitBus = this._massTransitProvider.GetRequiredService<IBusControl>();
        await this._massTransitBus.StartAsync();
        this._massTransitDispatcher = this._massTransitProvider.GetRequiredService<IWebhookDispatcher>();

        // ═════════════════════════════════════════════════════════════════════
        // C. Setup: Wiaoj Webhooks + WOLVERINE Transport
        // ═════════════════════════════════════════════════════════════════════
        this._wolverineHost = await Host.CreateDefaultBuilder()
            .ConfigureServices(services => {
                services.AddLogging();
                services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
                services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
                services.AddSingleton<IWebhookEndpointResolver>(resolver);

                services.AddWiaojWebhooks(w => {
                    w.Services.AddSingleton<IWebhookTransport, WolverineWebhookTransport>();
                });
            })
            .UseWolverine(opts => {
                opts.PublishMessage<WebhookDeliveryJob>().ToLocalQueue("webhook_jobs");
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
    // BENCHMARKS
    // ────────────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Webhooks + Tyto", OperationsPerInvoke = OperationsPerInvoke)]
    public async Task Webhooks_With_Tyto() {
        BenchmarkOrderEvent @event = new("ORD-BENCH-1", 150m);
        for(int i = 0; i < OperationsPerInvoke; i++) {
            await this._tytoDispatcher.DispatchAsync(this._endpointId, @event, CancellationToken.None);
        }
    }

    [Benchmark(Description = "Webhooks + MassTransit", OperationsPerInvoke = OperationsPerInvoke)]
    public async Task Webhooks_With_MassTransit() {
        BenchmarkOrderEvent @event = new("ORD-BENCH-1", 150m);
        for(int i = 0; i < OperationsPerInvoke; i++) {
            await this._massTransitDispatcher.DispatchAsync(this._endpointId, @event, CancellationToken.None);
        }
    }

    [Benchmark(Description = "Webhooks + Wolverine", OperationsPerInvoke = OperationsPerInvoke)]
    public async Task Webhooks_With_Wolverine() {
        BenchmarkOrderEvent @event = new("ORD-BENCH-1", 150m);
        for(int i = 0; i < OperationsPerInvoke; i++) {
            await this._wolverineDispatcher.DispatchAsync(this._endpointId, @event, CancellationToken.None);
        }
    }

    // ── Consumers ──
    private sealed class MassTransitWebhookJobConsumer(IServiceScopeFactory scopeFactory) : IConsumer<WebhookDeliveryJob> {
        public async Task Consume(ConsumeContext<WebhookDeliveryJob> context) {
            using IServiceScope scope = scopeFactory.CreateScope();
            IWebhookJobHandler handler = scope.ServiceProvider.GetRequiredService<IWebhookJobHandler>();
            await handler.HandleAsync(context.Message, context.CancellationToken);
        }
    }

    public sealed class WolverineWebhookJobHandler {
        public static async Task Handle(WebhookDeliveryJob job, IWebhookJobHandler handler, CancellationToken ct) {
            await handler.HandleAsync(job, ct);
        }
    }

    private sealed class InMemoryTestEndpointResolver : IWebhookEndpointResolver {
        private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = [];
        public void Register(WebhookEndpoint endpoint) {
            this._endpoints[endpoint.Id] = endpoint;
        }

        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
        }
    }
}