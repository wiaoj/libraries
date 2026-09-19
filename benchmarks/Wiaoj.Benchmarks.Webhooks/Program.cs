using MassTransit;
using MemoryPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using Tyto;
using Tyto.DependencyInjection;
using Wiaoj.Benchmarks.Webhooks;
using Wiaoj.Benchmarks.Webhooks.Transports;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Webhooks;
using Wolverine;

// ── 0. WILDCARD ASSEMBLY RESOLVER ──
AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
    string? requestedName = new System.Reflection.AssemblyName(args.Name).Name;
    if(requestedName != null && requestedName.StartsWith("Wiaoj.")) {
        Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == requestedName);
        if(loaded != null) return loaded;

        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{requestedName}.dll");
        if(File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);
    }
    return null;
};

Console.OutputEncoding = System.Text.Encoding.UTF8;

while(true) {
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║            TYTO ARENA (MEMORYPACK & DIRECT DISPATCH)        ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine("[1] 100k Grand Prix (Tyto vs Wolverine vs MassTransit)");
    Console.WriteLine("[2] 100k Unchained Showdown (Tyto MemoryPack vs Wolverine)");
    Console.WriteLine("[3] 1 Milyon Mesaj Cehennem Koşusu (Tyto MemoryPack vs Wolverine)");
    Console.WriteLine("[0] Çıkış");
    Console.Write("\nSeçiminiz: ");

    char key = Console.ReadKey().KeyChar;
    Console.WriteLine("\n");

    switch(key) {
        case '1':
            await Run100kHandicappedGrandPrix();
            break;
        case '2':
            await Run100kUnchainedShowdown();
            break;
        case '3':
            await Run1MillionSoakTest();
            break;
        case '0':
            return;
        default:
            Console.WriteLine("Geçersiz seçim!");
            break;
    }

    Console.WriteLine("\nMenüye dönmek için bir tuşa basın...");
    Console.ReadKey();
}


// ═══════════════════════════════════════════════════════════════════════════════
// SENARYO 1: MEVCUT 100k TESTİ (10 TUR)
// ═══════════════════════════════════════════════════════════════════════════════
async Task Run100kHandicappedGrandPrix() {
    const int count = 100_000;
    const int rounds = 10;
    ((IHost Host, IWebhookDispatcher Dispatcher) tytoH, (IBusControl Bus, IWebhookDispatcher Dispatcher) mtH, (IHost Host, IWebhookDispatcher Dispatcher) wolvH, BenchmarkOrderEvent? env, WebhookEndpointId epId) = await SetupStandardEnvironments();

    Console.WriteLine("🔥 Warmup yapılıyor...");
    await ExecuteWarmup(tytoH.Dispatcher, wolvH.Dispatcher, mtH.Dispatcher, epId, env);

    List<RunMetric> tytoM = [], wolvM = [], mtM = [];
    PrintRoundHeader();

    for(int r = 1; r <= rounds; r++) {
        RunMetric t = await RunRound(tytoH.Dispatcher, count, epId, env);
        tytoM.Add(t);
        PrintRoundRow(r, "Tyto", t);

        RunMetric w = await RunRound(wolvH.Dispatcher, count, epId, env);
        wolvM.Add(w);
        PrintRoundRow(r, "Wolverine", w);

        RunMetric m = await RunRound(mtH.Dispatcher, count, epId, env);
        mtM.Add(m);
        PrintRoundRow(r, "MassTransit", m);
        Console.WriteLine(new string('-', 95));
    }

    PrintSummary("10 TUR 100K ORTALAMA", ("Tyto", tytoM), ("Wolverine", wolvM), ("MassTransit", mtM));

    await tytoH.Host.StopAsync();
    await wolvH.Host.StopAsync();
    await mtH.Bus.StopAsync();
}


// ═══════════════════════════════════════════════════════════════════════════════
// SENARYO 2: 100K UNCHAINED (MEMORYPACK DESTEKLİ)
// ═══════════════════════════════════════════════════════════════════════════════
async Task Run100kUnchainedShowdown() {
    const int count = 100_000;
    const int rounds = 5;
    int concurrency = Environment.ProcessorCount;

    WebhookEndpointId epId = new("ep_bench");
    FakeSecretProtector<WebhookSigningContext> protector = new();
    InMemoryTestEndpointResolver resolver = new();
    resolver.Register(new WebhookEndpoint(epId, new Uri("http://localhost/bench"), protector.Protect("sec")));
    BenchmarkOrderEvent sampleEvent = new("ORD-1", 100m);

    // ── Tyto: MemoryPack Setup ──
    HostApplicationBuilder tytoBuilder = Host.CreateApplicationBuilder();
    tytoBuilder.Services.AddLogging(l => l.ClearProviders());
    tytoBuilder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
    tytoBuilder.Services.AddSingleton<IWebhookEndpointResolver>(resolver);
    tytoBuilder.Services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

    // MemoryPack bağlandı
    tytoBuilder.Services.AddWiaojSerializer(serializer => {
        serializer.UseMemoryPack<WebhookSerializerKey>();
        serializer.UseMemoryPack<TytoJsonSerializerKey>();
    });

    tytoBuilder.AddTyto(tyto => {
        tyto.MessageDefinitions(d => d.Add<UnchainedTytoEnvelope>("unchained.job", 1));

        tyto.Transports(t => {
            t.AddInMemory("memory", opt => {
                opt.DefaultConcurrencyLimit = concurrency;
                opt.FullMode = BoundedChannelFullMode.Wait;
                opt.ChannelCapacity = 100_000;
                opt.Bind("ex.unchained", "q.unchained");
            });
        });
        tyto.Endpoints(ep => {
            ep.Add("UNCHAINED-EP", e => {
                e.ListenOn("memory", "q.unchained");
                e.Routing.Publish<UnchainedTytoEnvelope>().To("memory", "ex.unchained");
                e.AddHandler<UnchainedTytoHandler>();
            });
        });
    });

    tytoBuilder.Services.AddWiaojWebhooks(w => {
        w.Services.AddSingleton<IWebhookTransport, UnchainedTytoTransport>();
        w.RegisterEvent<BenchmarkOrderEvent>("order.created");
    });

    IHost tytoHost = tytoBuilder.Build();
    await tytoHost.StartAsync();
    IWebhookDispatcher tytoDispatcher = tytoHost.Services.GetRequiredService<IWebhookDispatcher>();

    // ── Wolverine Setup ──
    IHost wolvHost = await SetupWolverine(protector, resolver, concurrency);
    IWebhookDispatcher wolvDispatcher = wolvHost.Services.GetRequiredService<IWebhookDispatcher>();

    Console.WriteLine("🔥 Isınma turu...");
    await ExecuteWarmup(tytoDispatcher, wolvDispatcher, null, epId, sampleEvent);

    List<RunMetric> tytoM = [], wolvM = [];
    PrintRoundHeader();

    for(int r = 1; r <= rounds; r++) {
        RunMetric t = await RunRound(tytoDispatcher, count, epId, sampleEvent);
        tytoM.Add(t);
        PrintRoundRow(r, "Tyto (MemoryPack)", t);

        RunMetric w = await RunRound(wolvDispatcher, count, epId, sampleEvent);
        wolvM.Add(w);
        PrintRoundRow(r, "Wolverine", w);
        Console.WriteLine(new string('-', 95));
    }

    PrintSummary("100K MEMORYPACK ORTALAMA", ("Tyto (MemoryPack)", tytoM), ("Wolverine", wolvM));

    await tytoHost.StopAsync();
    await wolvHost.StopAsync();
}


// ═══════════════════════════════════════════════════════════════════════════════
// SENARYO 3: 1 MİLYON MESAJ SOAK TESTİ (MEMORYPACK & DIRECT DISPATCH)
// ═══════════════════════════════════════════════════════════════════════════════
async Task Run1MillionSoakTest() {
    const int count = 1_000_000;
    int concurrency = Environment.ProcessorCount;

    WebhookEndpointId epId = new("ep_bench");
    FakeSecretProtector<WebhookSigningContext> protector = new();
    InMemoryTestEndpointResolver resolver = new();
    resolver.Register(new WebhookEndpoint(epId, new Uri("http://localhost/bench"), protector.Protect("sec")));
    BenchmarkOrderEvent sampleEvent = new("ORD-MILLION", 500m);

    // Tyto (MemoryPack) Setup
    HostApplicationBuilder tytoBuilder = Host.CreateApplicationBuilder();
    tytoBuilder.Services.AddLogging(l => l.ClearProviders());
    tytoBuilder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
    tytoBuilder.Services.AddSingleton<IWebhookEndpointResolver>(resolver);
    tytoBuilder.Services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

    // ── GÜNCELLENDİ: MessagePack yerine MemoryPack ──
    tytoBuilder.Services.AddWiaojSerializer(serializer => {
        serializer.UseMemoryPack<WebhookSerializerKey>();
        serializer.UseMemoryPack<TytoJsonSerializerKey>();
    });

    tytoBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
        ["Tyto:Pools:SendMessageContext:MaximumRetained"] = "50000",
        ["Tyto:Pools:ReceiveMessageContext:MaximumRetained"] = "50000",
        ["Tyto:Pools:OutgoingMessage:MaximumRetained"] = "50000",
        ["Tyto:Pools:HeadersDictionary:MaximumRetained"] = "50000",
        ["Tyto:Pools:HandlerEventData:MaximumRetained"] = "50000",
        ["Tyto:Pools:ArrayBufferWriter:MaximumRetained"] = "50000",
        ["Tyto:Pools:ArrayBufferWriter:InitialCapacity"] = "1024",
        ["Tyto:Pools:ArrayBufferWriter:MaxRetainedCapacity"] = "65536"
    });

    tytoBuilder.AddTyto(tyto => {
        tyto.MessageDefinitions(d => d.Add<UnchainedTytoEnvelope>("unchained.job", 1));

        tyto.Transports(t => {
            t.AddInMemory("memory", opt => {
                opt.DefaultConcurrencyLimit = concurrency;
                opt.FullMode = BoundedChannelFullMode.Wait;
                opt.ChannelCapacity = 1_000_000;
                opt.Bind("ex.unchained", "q.unchained");
            });
        });
        tyto.Endpoints(ep => {
            ep.Add("UNCHAINED-EP", e => {
                e.ListenOn("memory", "q.unchained");
                e.Routing.Publish<UnchainedTytoEnvelope>().To("memory", "ex.unchained");
                e.AddHandler<UnchainedTytoHandler>();
            });
        });
    });

    tytoBuilder.Services.AddWiaojWebhooks(w => {
        w.Services.AddSingleton<IWebhookTransport, UnchainedTytoTransport>();
        w.RegisterEvent<BenchmarkOrderEvent>("order.created");
    });

    IHost tytoHost = tytoBuilder.Build();
    await tytoHost.StartAsync();
    IWebhookDispatcher tytoDispatcher = tytoHost.Services.GetRequiredService<IWebhookDispatcher>();

    IHost wolvHost = await SetupWolverineUnchained(protector, resolver, concurrency);
    IWebhookDispatcher wolvDispatcher = wolvHost.Services.GetRequiredService<IWebhookDispatcher>();

    Console.WriteLine("🔥 Isınma turu yapılıyor (10.000 mesaj)...");
    await ExecuteWarmup(tytoDispatcher, wolvDispatcher, null, epId, sampleEvent);

    Console.WriteLine("🔥 Isınma turu yapılıyor (100.000 mesaj)...");
    await ExecuteWarmup(tytoDispatcher, wolvDispatcher, null, epId, sampleEvent, 100_000);

    Console.WriteLine("🔥 1 Milyon Mesaj Testi Başlıyor (MemoryPack & Direct Dispatch)...");

    PrintRoundHeader();

    RunMetric tMetric = await RunRound(tytoDispatcher, count, epId, sampleEvent);
    PrintRoundRow(1, "Tyto (MemoryPack 1M)", tMetric);

    RunMetric wMetric = await RunRound(wolvDispatcher, count, epId, sampleEvent);
    PrintRoundRow(1, "Wolverine (1M)", wMetric);

    Console.WriteLine(new string('=', 95));

    await tytoHost.StopAsync();
    await wolvHost.StopAsync();
}


// ═══════════════════════════════════════════════════════════════════════════════
// ORTAK YARDIMCI VE KURULUM METOTLARI
// ═══════════════════════════════════════════════════════════════════════════════

async Task<RunMetric> RunRound(IWebhookDispatcher dispatcher, int count, WebhookEndpointId epId, BenchmarkOrderEvent evt) {
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long allocBefore = GC.GetTotalAllocatedBytes(true);
    int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);

    Stopwatch sw = Stopwatch.StartNew();
    BenchmarkCompletionTracker.Reset(count);
    for(int i = 0; i < count; i++) {
        await dispatcher.DispatchAsync(epId, evt, CancellationToken.None);
    }
    await BenchmarkCompletionTracker.WaitForCompletionAsync(TimeSpan.FromSeconds(120));
    sw.Stop();

    long allocMb = (GC.GetTotalAllocatedBytes(true) - allocBefore) / 1024 / 1024;
    return new RunMetric(
        sw.ElapsedMilliseconds,
        count / sw.Elapsed.TotalSeconds,
        allocMb,
        GC.CollectionCount(0) - g0,
        GC.CollectionCount(1) - g1,
        GC.CollectionCount(2) - g2
    );
}

async Task ExecuteWarmup(IWebhookDispatcher t, IWebhookDispatcher w, IWebhookDispatcher? m, WebhookEndpointId epId, BenchmarkOrderEvent evt, int count = 10_000) {
    BenchmarkCompletionTracker.Reset(count);
    for(int i = 0; i < count; i++) await t.DispatchAsync(epId, evt, CancellationToken.None);
    await BenchmarkCompletionTracker.WaitForCompletionAsync();

    BenchmarkCompletionTracker.Reset(10000);
    for(int i = 0; i < count; i++) await w.DispatchAsync(epId, evt, CancellationToken.None);
    await BenchmarkCompletionTracker.WaitForCompletionAsync();

    if(m != null) {
        BenchmarkCompletionTracker.Reset(count);
        for(int i = 0; i < count; i++) await m.DispatchAsync(epId, evt, CancellationToken.None);
        await BenchmarkCompletionTracker.WaitForCompletionAsync();
    }
}

async Task<IHost> SetupWolverine(FakeSecretProtector<WebhookSigningContext> p, InMemoryTestEndpointResolver r, int conc) {
    return await Host.CreateDefaultBuilder()
        .ConfigureServices(services => {
            services.AddLogging(l => l.ClearProviders());
            services.AddSingleton<ISecretProtector<WebhookSigningContext>>(p);
            services.AddSingleton<IWebhookEndpointResolver>(r);
            services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();
            services.AddWiaojSerializer(s => s.UseMemoryPack<WebhookSerializerKey>());
            services.AddWiaojWebhooks(w => {
                w.Services.AddSingleton<IWebhookTransport, WolverineWebhookTransport>();
                w.RegisterEvent<BenchmarkOrderEvent>("order.created");
            });
        })
        .UseWolverine(opts => {
            opts.PublishMessage<WebhookDeliveryJob>()
                .ToLocalQueue("webhook_jobs")
                .BufferedInMemory()
                .MaximumParallelMessages(conc * 2);
        })
        .StartAsync();
}

async Task<IHost> SetupWolverineUnchained(FakeSecretProtector<WebhookSigningContext> p, InMemoryTestEndpointResolver r, int conc) {
    return await Host.CreateDefaultBuilder()
        .ConfigureServices(services => {
            services.AddLogging(l => l.ClearProviders());
            services.AddSingleton<ISecretProtector<WebhookSigningContext>>(p);
            services.AddSingleton<IWebhookEndpointResolver>(r);
            services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

            // Wiaoj Serializer MemoryPack
            services.AddWiaojSerializer(s => s.UseMemoryPack<WebhookSerializerKey>());

            services.AddWiaojWebhooks(w => {
                w.Services.AddSingleton<IWebhookTransport, WolverineUnchainedTransport>();
                w.RegisterEvent<BenchmarkOrderEvent>("order.created");
            });
        })
        .UseWolverine(opts => {
            // Local Queue'yu Tyto ile aynı concurrency parametrelerine çekiyoruz:
            opts.PublishMessage<WolverineUnchainedEnvelope>()
                .ToLocalQueue("unchained_jobs")
                .BufferedInMemory()
                .MaximumParallelMessages(conc); // Tyto DefaultConcurrencyLimit ile birebir aynı yap
        })
        .StartAsync();
}

async Task<((IHost Host, IWebhookDispatcher Dispatcher) Tyto, (IBusControl Bus, IWebhookDispatcher Dispatcher) MT, (IHost Host, IWebhookDispatcher Dispatcher) Wolv, BenchmarkOrderEvent Evt, WebhookEndpointId EpId)> SetupStandardEnvironments() {
    int conc = Environment.ProcessorCount;
    WebhookEndpointId epId = new("ep_benchmark");
    FakeSecretProtector<WebhookSigningContext> protector = new();
    InMemoryTestEndpointResolver resolver = new();
    resolver.Register(new WebhookEndpoint(epId, new Uri("http://localhost/bench"), protector.Protect("whsec_secret")));
    BenchmarkOrderEvent evt = new("ORD-1", 250m);

    // Tyto (Standard)
    HostApplicationBuilder tb = Host.CreateApplicationBuilder();
    tb.Services.AddLogging(l => l.ClearProviders());
    tb.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
    tb.Services.AddSingleton<IWebhookEndpointResolver>(resolver);
    tb.Services.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();

    tb.Services.AddWiaojSerializer(serializer => {
        serializer.UseMemoryPack<WebhookSerializerKey>();
        serializer.UseMemoryPack<TytoJsonSerializerKey>();
    });

    tb.AddTyto(t => {
        t.MessageDefinitions(d => d.Add<TytoWebhookJobEnvelope>("webhook.delivery.job", 1));
        t.Transports(tr => tr.AddInMemory("memory", opt => {
            opt.DefaultConcurrencyLimit = conc;
            opt.FullMode = BoundedChannelFullMode.Wait;
            opt.ChannelCapacity = 100_000;
            opt.Bind("ex.webhook.jobs", "q.webhook.jobs");
        }));
        t.Endpoints(ep => ep.Add("WEBHOOK-DISPATCH-EP", e => {
            e.ListenOn("memory", "q.webhook.jobs");
            e.Routing.Publish<TytoWebhookJobEnvelope>().To("memory", "ex.webhook.jobs");
            e.AddHandler<TytoWebhookJobHandler>();
        }));
    });
    tb.Services.AddWiaojWebhooks(w => {
        w.Services.AddSingleton<IWebhookTransport, TytoWebhookTransport>();
        w.RegisterEvent<BenchmarkOrderEvent>("order.created");
    });
    IHost th = tb.Build();
    await th.StartAsync();

    // MT
    ServiceCollection ms = new();
    ms.AddLogging(l => l.ClearProviders());
    ms.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);
    ms.AddSingleton<IWebhookEndpointResolver>(resolver);
    ms.AddSingleton<IWebhookJobHandler, NoOpWebhookJobHandler>();
    ms.AddWiaojSerializer(s => s.UseMemoryPack<WebhookSerializerKey>());

    ms.AddMassTransit(x => {
        x.AddConsumer<MassTransitWebhookJobConsumer>();
        x.UsingInMemory((ctx, cfg) => { cfg.ConcurrentMessageLimit = conc; cfg.ConfigureEndpoints(ctx); });
    });
    ms.AddWiaojWebhooks(w => {
        w.Services.AddSingleton<IWebhookTransport, MassTransitWebhookTransport>();
        w.RegisterEvent<BenchmarkOrderEvent>("order.created");
    });
    ServiceProvider mp = ms.BuildServiceProvider();
    IBusControl mb = mp.GetRequiredService<IBusControl>();
    await mb.StartAsync();

    // Wolverine
    IHost wh = await SetupWolverine(protector, resolver, conc);

    return ((th, th.Services.GetRequiredService<IWebhookDispatcher>()),
            (mb, mp.GetRequiredService<IWebhookDispatcher>()),
            (wh, wh.Services.GetRequiredService<IWebhookDispatcher>()),
            evt, epId);
}

void PrintRoundHeader() {
    Console.WriteLine("{0,-6} | {1,-26} | {2,-10} | {3,-15} | {4,-10} | {5}",
        "Tur", "Framework", "Süre (ms)", "Hız (Msg/Sn)", "Alloc (MB)", "GC (Gen 0/1/2)");
    Console.WriteLine(new string('-', 95));
}
void PrintRoundRow(int round, string name, RunMetric m) {
    Console.WriteLine("{0,-6} | {1,-26} | {2,-10:N0} | {3,-15:N0} | {4,-10} | {5}/{6}/{7}",
        $"#{round}", name, m.ElapsedMs, m.Speed, $"{m.AllocMb} MB", m.Gen0, m.Gen1, m.Gen2);
}
void PrintSummary(string title, params (string Name, List<RunMetric> M)[] items) {
    Console.WriteLine($"\n==================== {title} ====================");
    Console.WriteLine("{0,-26} | {1,-12} | {2,-15} | {3,-10} | {4}", "Framework", "Ort. Süre", "Ort. Hız", "Ort. Alloc", "Ort. GC (0/1/2)");
    Console.WriteLine(new string('-', 95));
    foreach((string? Name, List<RunMetric>? M) in items) {
        Console.WriteLine("{0,-26} | {1,-12:N1} | {2,-15:N0} | {3,-10} | {4:F1}/{5:F1}/{6:F1}",
            Name,
            $"{M.Average(x => x.ElapsedMs):N1} ms",
            M.Average(x => x.Speed),
            $"{M.Average(x => x.AllocMb):N0} MB",
            M.Average(x => x.Gen0), M.Average(x => x.Gen1), M.Average(x => x.Gen2));
    }
    Console.WriteLine(new string('=', 95));
}

// ── UNCHAINED TYTO TRANSPORTS ──
// ── GÜNCELLENDİ: MemoryPackable + partial ──
[Message("unchained.job", 1)]
[MemoryPackable]
public partial record UnchainedTytoEnvelope(
    string JobId,
    string EndpointId,
    string PartitionKey,
    string EventType,
    BenchmarkOrderEvent Payload) : IEvent;

public sealed class UnchainedTytoHandler(IWebhookJobHandler jobHandler) : IEventHandler<UnchainedTytoEnvelope> {
    public async ValueTask HandleAsync(IMessageContext<UnchainedTytoEnvelope> context, CancellationToken cancellationToken = default) {
        UnchainedTytoEnvelope msg = context.Message;

        // Doğrudan constructor çağrısıyla parse yükü hafifletildi
        WebhookDeliveryJob job = new(
            WebhookJobId.Parse(msg.JobId),
            new WebhookEndpointId(msg.EndpointId),
            new WebhookPartitionKey(msg.PartitionKey),
            msg.EventType,
            msg.Payload);

        await jobHandler.HandleAsync(job, cancellationToken).ConfigureAwait(false);
        BenchmarkCompletionTracker.SignalItemCompleted();
    }
}

public sealed class UnchainedTytoTransport(Tyto.IBus bus) : IWebhookTransport {
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        UnchainedTytoEnvelope env = new(
            job.Id.Value,
            job.EndpointId.Value,
            job.PartitionKey.Value,
            job.EventType,
            (BenchmarkOrderEvent)job.Payload);

        return bus.PublishAsync(env, cancellationToken).AsTask();
    }
    public Task EnqueueAsync(WebhookDeliveryJob job) {
        return EnqueueAsync(job, CancellationToken.None);
    }

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) {
        return EnqueueAsync(job, CancellationToken.None);
    }

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        return EnqueueAsync(job, cancellationToken);
    }

    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}

public record RunMetric(long ElapsedMs, double Speed, long AllocMb, int Gen0, int Gen1, int Gen2);

// ── GÜNCELLENDİ: MemoryPackable + partial ──
[MemoryPackable]
public partial record BenchmarkOrderEvent(string OrderId, decimal Amount) : IWebhookEvent;

public sealed class NoOpWebhookJobHandler : IWebhookJobHandler {
    private static readonly Task<WebhookDeliveryAttempt> Cached = Task.FromResult(
        new WebhookDeliveryAttempt(new WebhookEndpointId("ep_bench"), 1, UnixTimestamp.Now, TimeSpan.Zero, WebhookDeliveryResult.Success()));
    public Task<WebhookDeliveryAttempt> HandleAsync(WebhookDeliveryJob job, CancellationToken ct = default) {
        return Cached;
    }
}
public sealed class InMemoryTestEndpointResolver : IWebhookEndpointResolver {
    private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = [];
    public void Register(WebhookEndpoint endpoint) {
        this._endpoints[endpoint.Id] = endpoint;
    }

    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken ct = default) {
        return ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
    }
}

[MemoryPackable]
public partial record WolverineUnchainedEnvelope(
    string JobId,
    string EndpointId,
    string PartitionKey,
    string EventType,
    BenchmarkOrderEvent Payload);

// Wolverine Handler'ı: Tyto ile BİREBİR AYNI İŞİ YAPACAK
public static class WolverineUnchainedHandler {
    public static async ValueTask Handle(
        WolverineUnchainedEnvelope msg,
        IWebhookJobHandler jobHandler,
        CancellationToken ct) {

        // Tyto'da yaptığın nesne oluşturma masrafının aynısı:
        WebhookDeliveryJob job = new(
            WebhookJobId.Parse(msg.JobId),
            new WebhookEndpointId(msg.EndpointId),
            new WebhookPartitionKey(msg.PartitionKey),
            msg.EventType,
            msg.Payload);

        await jobHandler.HandleAsync(job, ct).ConfigureAwait(false);
        BenchmarkCompletionTracker.SignalItemCompleted();
    }
}

// Wolverine Transport'u:
public sealed class WolverineUnchainedTransport(IMessageBus bus) : IWebhookTransport {
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        WolverineUnchainedEnvelope env = new(
            job.Id.Value,
            job.EndpointId.Value,
            job.PartitionKey.Value,
            job.EventType,
            (BenchmarkOrderEvent)job.Payload);

        return bus.PublishAsync(env).AsTask();
    }

    public Task EnqueueAsync(WebhookDeliveryJob job) {
        return EnqueueAsync(job, CancellationToken.None);
    }

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) {
        return EnqueueAsync(job, CancellationToken.None);
    }

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        return EnqueueAsync(job, cancellationToken);
    }

    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}