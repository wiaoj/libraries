using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wiaoj.Benchmarks.Mediator;
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ComprehensiveBenchmarks {
    private IServiceProvider _providerWiaojFast = null!;
    private IServiceProvider _providerWiaojTrace = null!;
    private IServiceProvider _providerWiaojBehavior = null!;
    private IServiceProvider _providerMediatR = null!;

    // Objeler
    private readonly Ping _ping = new();
    private readonly StreamPing _streamPing = new();
    private readonly PolyPing _polyPing = new(); // Base türden türetilmiş
    private readonly ErrorPing _errorPing = new();

    // MediatR Objeler
    private readonly MediatR_Ping _mPing = new();
    private readonly MediatR_StreamPing _mStreamPing = new();

    [GlobalSetup]
    public void Setup() {
        // ----------------------------------------------------------------
        // 1. WIAOJ: FAST (Saf Hız - Trace Yok, Behavior Yok)
        // ----------------------------------------------------------------
        ServiceCollection servicesFast = new();
        servicesFast.AddMediator(cfg => {
            cfg.WithDefaultLifetime(ServiceLifetime.Singleton);
            cfg.RegisterHandler<PingHandler>();
            cfg.RegisterHandler<StreamPingHandler>();
            cfg.RegisterHandler<PolyPingHandler>(); // Base Handler
            cfg.RegisterHandler<ErrorPingHandler>();
        });
        this._providerWiaojFast = servicesFast.BuildServiceProvider();

        // ----------------------------------------------------------------
        // 2. WIAOJ: TRACING (OpenTelemetry Açık)
        // ----------------------------------------------------------------
        ServiceCollection servicesTrace = new();
        servicesTrace.AddMediator(cfg => {
            cfg.WithDefaultLifetime(ServiceLifetime.Singleton);
            cfg.WithOpenTelemetry(); // <--- TRACE AKTİF
            cfg.RegisterHandler<PingHandler>();
        });
        this._providerWiaojTrace = servicesTrace.BuildServiceProvider();

        // ----------------------------------------------------------------
        // 3. WIAOJ: PIPELINE (1 Adet Behavior Ekli)
        // ----------------------------------------------------------------
        ServiceCollection servicesBehavior = new();
        servicesBehavior.AddMediator(cfg => {
            cfg.WithDefaultLifetime(ServiceLifetime.Singleton);
            cfg.RegisterHandler<PingHandler>();
            cfg.AddOpenBehavior(typeof(NopBehavior<,>)); // <--- 1 Behavior Ekli
        });
        this._providerWiaojBehavior = servicesBehavior.BuildServiceProvider();

        // ----------------------------------------------------------------
        // 4. MEDIATR (Rakip)
        // ----------------------------------------------------------------
        ServiceCollection servicesMediatR = new();
        servicesMediatR.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ComprehensiveBenchmarks).Assembly));
        this._providerMediatR = servicesMediatR.BuildServiceProvider();

        // --- Cache Warmup (Isınma Turları) ---
        // İlk isteklerde Expression Tree derlendiği için yavaştır. Cache'leri dolduruyoruz.
        this._providerWiaojFast.GetRequiredService<Wiaoj.Mediator.IMediator>().Send(this._ping).GetAwaiter().GetResult();
        this._providerWiaojFast.GetRequiredService<Wiaoj.Mediator.IMediator>().Send(this._polyPing).GetAwaiter().GetResult(); // Poly Cache dolsun
    }

    // ========================================================================
    // KATEGORİ 1: KAFA KAFAYA (Wiaoj Fast vs MediatR)
    // ========================================================================

    [Benchmark(Baseline = true, Description = "1. Wiaoj: Send (Fast)")]
    [BenchmarkCategory("Head-to-Head")]
    public Task Wiaoj_Send() {
        var mediator = this._providerWiaojFast.GetRequiredService<Wiaoj.Mediator.IMediator>();
        return mediator.Send(this._ping);
    }

    [Benchmark(Description = "2. MediatR: Send")]
    [BenchmarkCategory("Head-to-Head")]
    public Task MediatR_Send() {
        var mediator = this._providerMediatR.GetRequiredService<MediatR.IMediator>();
        return mediator.Send(this._mPing);
    }

    [Benchmark(Description = "3. Wiaoj: Stream (Iterate)")]
    [BenchmarkCategory("Head-to-Head")]
    public async Task Wiaoj_Stream() {
        var mediator = this._providerWiaojFast.GetRequiredService<Wiaoj.Mediator.IMediator>();
        var stream = mediator.CreateStream(this._streamPing);
        await foreach(var item in stream) { /* Tüket */ }
    }

    [Benchmark(Description = "4. MediatR: Stream (Iterate)")]
    [BenchmarkCategory("Head-to-Head")]
    public async Task MediatR_Stream() {
        var mediator = this._providerMediatR.GetRequiredService<MediatR.IMediator>();
        var stream = mediator.CreateStream(this._mStreamPing);
        await foreach(var item in stream) { /* Tüket */ }
    }

    // ========================================================================
    // KATEGORİ 2: WIAOJ ÖZELLİK ANALİZİ (Maliyet Ölçümü)
    // ========================================================================

    [Benchmark(Description = "Feature: Tracing Overhead")]
    [BenchmarkCategory("Wiaoj-DeepDive")]
    public Task Wiaoj_Tracing() {
        // Trace açıldığında ne kadar yavaşlıyoruz?
        var mediator = this._providerWiaojTrace.GetRequiredService<Wiaoj.Mediator.IMediator>();
        return mediator.Send(this._ping);
    }

    [Benchmark(Description = "Feature: 1 Behavior Overhead")]
    [BenchmarkCategory("Wiaoj-DeepDive")]
    public Task Wiaoj_Behavior() {
        // Pipeline'a 1 behavior eklenince ne kadar yavaşlıyoruz?
        var mediator = this._providerWiaojBehavior.GetRequiredService<Wiaoj.Mediator.IMediator>();
        return mediator.Send(this._ping);
    }

    [Benchmark(Description = "Feature: Polymorphic Dispatch")]
    [BenchmarkCategory("Wiaoj-DeepDive")]
    public Task Wiaoj_Polymorphic() {
        // Base class üzerinden handler bulma hızı (Cached)
        var mediator = this._providerWiaojFast.GetRequiredService<Wiaoj.Mediator.IMediator>();
        return mediator.Send(this._polyPing);
    }

    [Benchmark(Description = "Feature: Exception Handling")]
    [BenchmarkCategory("Wiaoj-DeepDive")]
    public async Task Wiaoj_Exception() {
        // Derlenmiş Try-Catch bloğunun hızı
        var mediator = this._providerWiaojFast.GetRequiredService<Wiaoj.Mediator.IMediator>();
        try {
            await mediator.Send(this._errorPing);
        }
        catch { /* Yut */ }
    }
}

// ============================================================================
// --- TANIMLAMALAR (WIAOJ) ---
// ============================================================================

public class Ping : Wiaoj.Mediator.IRequest<string> { }
public class PolyBase : Wiaoj.Mediator.IRequest<string> { }
public class PolyPing : PolyBase { } // Base'den türüyor
public class StreamPing : Wiaoj.Mediator.IStreamRequest<int> { }
public class ErrorPing : Wiaoj.Mediator.IRequest<WiaojUnit> { }
public struct WiaojUnit { }

// Handlers
public class PingHandler : Wiaoj.Mediator.IRequestHandler<Ping, string> {
    public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken) {
        return Task.FromResult("Pong");
    }
}

public class PolyPingHandler : Wiaoj.Mediator.IRequestHandler<PolyBase, string> {
    public Task<string> HandleAsync(PolyBase request, CancellationToken cancellationToken) {
        return Task.FromResult("PolyPong");
    }
}

public class StreamPingHandler : Wiaoj.Mediator.IStreamRequestHandler<StreamPing, int> {
    public async IAsyncEnumerable<int> Handle(StreamPing request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) {
        for(int i = 0; i < 5; i++) { yield return i; }
        await Task.CompletedTask;
    }
}

public class ErrorPingHandler : Wiaoj.Mediator.IRequestHandler<ErrorPing, WiaojUnit> {
    public Task<WiaojUnit> HandleAsync(ErrorPing request, CancellationToken cancellationToken) {
        throw new InvalidOperationException("Boom");
    }
}

// Behavior
public class NopBehavior<TRequest, TResponse> : Wiaoj.Mediator.IPipelineBehavior<TRequest, TResponse> where TRequest : Wiaoj.Mediator.IRequest<TResponse> {
    public Task<TResponse> Handle(TRequest request, Wiaoj.Mediator.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default) {
        return next(); // Hiçbir şey yapmadan devam et
    }
}

// ============================================================================
// --- TANIMLAMALAR (MEDIATR) ---
// ============================================================================

public class MediatR_Ping : MediatR.IRequest<string> { }
public class MediatR_StreamPing : MediatR.IStreamRequest<int> { }

public class MediatR_PingHandler : MediatR.IRequestHandler<MediatR_Ping, string> {
    public Task<string> Handle(MediatR_Ping request, CancellationToken cancellationToken) {
        return Task.FromResult("Pong");
    }
}

public class MediatR_StreamPingHandler : MediatR.IStreamRequestHandler<MediatR_StreamPing, int> {
    public async IAsyncEnumerable<int> Handle(MediatR_StreamPing request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) {
        for(int i = 0; i < 5; i++) { yield return i; }
        await Task.CompletedTask;
    } 
}