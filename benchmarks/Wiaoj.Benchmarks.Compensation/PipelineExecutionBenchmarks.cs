using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wiaoj.Compensation;

namespace Wiaoj.Benchmarks.Compensation;

// ============================================================================
// BÖLÜM 0: Ortak context ve step tanımları
// ============================================================================
[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PipelineExecutionBenchmarks {
    #region Benchmark Context & Steps

    public sealed class BenchmarkContext {
        public int Value { get; set; }
    }

    private sealed class BenchmarkClassStep : ICompensationStep<BenchmarkContext> {
        public ValueTask ExecuteAsync(BenchmarkContext context, CancellationToken cancellationToken) {
            context.Value++;
            return ValueTask.CompletedTask;
        }

        public ValueTask CompensateAsync(BenchmarkContext context, CancellationToken cancellationToken) {
            context.Value--;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FaultyBenchmarkStep : ICompensationStep<BenchmarkContext> {
        public ValueTask ExecuteAsync(BenchmarkContext context, CancellationToken cancellationToken) {
            throw new InvalidOperationException("Simulated benchmark failure.");
        }

        public ValueTask CompensateAsync(BenchmarkContext context, CancellationToken cancellationToken) {
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    private ICompensationPipeline<BenchmarkContext> _emptyPipeline = null!;
    private ICompensationPipeline<BenchmarkContext> _classPipeline3Steps = null!;
    private ICompensationPipeline<BenchmarkContext> _lambdaPipeline3Steps = null!;
    private ICompensationPipeline<BenchmarkContext> _classPipeline10Steps = null!;
    private ICompensationPipeline<BenchmarkContext> _classPipeline20Steps = null!;
    private ICompensationPipeline<BenchmarkContext> _faultedClassPipeline = null!;
    private ICompensationPipeline<BenchmarkContext> _faultedLambdaPipeline = null!;
    private ICompensationPipeline<BenchmarkContext> _faultedPipeline10Steps = null!;

    [GlobalSetup]
    public void Setup() {
        this._emptyPipeline = new CompensationPipeline<BenchmarkContext>();

        this._classPipeline3Steps = new CompensationPipeline<BenchmarkContext>()
            .AddStep(new BenchmarkClassStep())
            .AddStep(new BenchmarkClassStep())
            .AddStep(new BenchmarkClassStep());

        this._lambdaPipeline3Steps = new CompensationPipeline<BenchmarkContext>()
            .AddStep(
                name: "Step_1",
                execute: (ctx, ct) => { ctx.Value++; return ValueTask.CompletedTask; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; })
            .AddStep(
                name: "Step_2",
                execute: (ctx, ct) => { ctx.Value++; return ValueTask.CompletedTask; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; })
            .AddStep(
                name: "Step_3",
                execute: (ctx, ct) => { ctx.Value++; return ValueTask.CompletedTask; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; });

        CompensationPipeline<BenchmarkContext> tenStepsPipeline = new();
        for(int i = 0; i < 10; i++) {
            tenStepsPipeline.AddStep(new BenchmarkClassStep());
        }
        this._classPipeline10Steps = tenStepsPipeline;

        CompensationPipeline<BenchmarkContext> twentyStepsPipeline = new();
        for(int i = 0; i < 20; i++) {
            twentyStepsPipeline.AddStep(new BenchmarkClassStep());
        }
        this._classPipeline20Steps = twentyStepsPipeline;

        this._faultedClassPipeline = new CompensationPipeline<BenchmarkContext>()
            .AddStep(new BenchmarkClassStep())
            .AddStep(new BenchmarkClassStep())
            .AddStep(new FaultyBenchmarkStep());

        this._faultedLambdaPipeline = new CompensationPipeline<BenchmarkContext>()
            .AddStep(
                name: "Step_1",
                execute: (ctx, ct) => { ctx.Value++; return ValueTask.CompletedTask; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; })
            .AddStep(
                name: "Step_2",
                execute: (ctx, ct) => { ctx.Value++; return ValueTask.CompletedTask; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; })
            .AddStep(
                name: "Faulty_Step",
                execute: (ctx, ct) => throw new InvalidOperationException("Faulted."));

        // 9 başarılı + 1 patlayan (10 adımda rollback maliyetinin nasıl büyüdüğünü görmek için)
        CompensationPipeline<BenchmarkContext> faulted10 = new();
        for(int i = 0; i < 9; i++) {
            faulted10.AddStep(new BenchmarkClassStep());
        }
        faulted10.AddStep(new FaultyBenchmarkStep());
        this._faultedPipeline10Steps = faulted10;
    }

    // ========================================================================
    // 1. HAPPY PATH — Pipeline
    // ========================================================================

    [Benchmark(Baseline = true, Description = "Base Overhead (0 Steps)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> Empty_Pipeline() {
        BenchmarkContext context = new();
        return await this._emptyPipeline.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "Happy Path (3 Class Steps)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> HappyPath_Class_3Steps() {
        BenchmarkContext context = new();
        return await this._classPipeline3Steps.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "Happy Path (3 Lambda Steps)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> HappyPath_Lambda_3Steps() {
        BenchmarkContext context = new();
        return await this._lambdaPipeline3Steps.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "Happy Path (10 Class Steps)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> HappyPath_Class_10Steps() {
        BenchmarkContext context = new();
        return await this._classPipeline10Steps.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "Happy Path (20 Class Steps)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> HappyPath_Class_20Steps() {
        BenchmarkContext context = new();
        return await this._classPipeline20Steps.RunAsync(context, CancellationToken.None);
    }

    // ========================================================================
    // 2. FAULTED & ROLLBACK — Pipeline (LIFO Compensation)
    // ========================================================================

    [Benchmark(Description = "Faulted & Rollback (Class Steps, 3)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> Faulted_Class_Rollback() {
        BenchmarkContext context = new();
        return await this._faultedClassPipeline.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "Faulted & Rollback (Lambda Steps, 3)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> Faulted_Lambda_Rollback() {
        BenchmarkContext context = new();
        return await this._faultedLambdaPipeline.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "Faulted & Rollback (Class Steps, 10)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> Faulted_Class_Rollback_10Steps() {
        BenchmarkContext context = new();
        return await this._faultedPipeline10Steps.RunAsync(context, CancellationToken.None);
    }

    // ========================================================================
    // 3. HOOK OVERHEAD
    // ========================================================================

    [Benchmark(Description = "Faulted + OnStepCompensated Hook")]
    public async ValueTask<CompensationReport<BenchmarkContext>> Faulted_With_Hook() {
        BenchmarkContext context = new();
        return await this._faultedClassPipeline.RunAsync(
            context,
            onCompensationFailed: null,
            onStepCompensated: (stepName, ctx) => ValueTask.CompletedTask,
            cancellationToken: CancellationToken.None);
    }

    // ========================================================================
    // 4. KARŞILAŞTIRMA — Elle yazılmış try/catch/finally (manuel rollback)
    //
    // Amaç: "Kütüphaneyi kullanmanın maliyeti, kendi elimle yazdığım
    // rollback koduna göre ne kadar?" sorusuna cevap vermek.
    // Bu manuel implementasyon, gerçek kullanıcıların pipeline yerine
    // yazacağı en tipik "spagetti kod" versiyonunu taklit ediyor:
    // bir Stack<Action> ile geri alma aksiyonlarını tutan klasik desen.
    // ========================================================================

    [Benchmark(Description = "[Manual] Happy Path (3 Steps, try/catch)")]
    public BenchmarkContext Manual_HappyPath_3Steps() {
        var context = new BenchmarkContext();
        var rollbackActions = new Stack<Action<BenchmarkContext>>();

        try {
            context.Value++;
            rollbackActions.Push(c => c.Value--);

            context.Value++;
            rollbackActions.Push(c => c.Value--);

            context.Value++;
            rollbackActions.Push(c => c.Value--);
        }
        catch {
            while(rollbackActions.Count > 0) {
                rollbackActions.Pop()(context);
            }
            throw;
        }

        return context;
    }

    [Benchmark(Description = "[Manual] Faulted & Rollback (3 Steps, try/catch)")]
    public BenchmarkContext Manual_Faulted_Rollback_3Steps() {
        var context = new BenchmarkContext();
        var rollbackActions = new Stack<Action<BenchmarkContext>>();

        try {
            context.Value++;
            rollbackActions.Push(c => c.Value--);

            context.Value++;
            rollbackActions.Push(c => c.Value--);

            // 3. adım patlıyor (pipeline senaryosundaki FaultyBenchmarkStep ile birebir eşleşiyor)
            throw new InvalidOperationException("Simulated benchmark failure.");
        }
        catch(InvalidOperationException) {
            while(rollbackActions.Count > 0) {
                rollbackActions.Pop()(context);
            }
        }

        return context;
    }

    [Benchmark(Description = "[Manual] Faulted & Rollback (10 Steps, try/catch)")]
    public BenchmarkContext Manual_Faulted_Rollback_10Steps() {
        var context = new BenchmarkContext();
        var rollbackActions = new Stack<Action<BenchmarkContext>>();

        try {
            for(int i = 0; i < 9; i++) {
                context.Value++;
                rollbackActions.Push(c => c.Value--);
            }
            throw new InvalidOperationException("Simulated benchmark failure.");
        }
        catch(InvalidOperationException) {
            while(rollbackActions.Count > 0) {
                rollbackActions.Pop()(context);
            }
        }

        return context;
    }

    // ========================================================================
    // 5. GERÇEKÇİ SENARYO — I/O-bound step'ler (network/disk simülasyonu)
    //
    // Amaç: Mikrosaniye seviyesindeki farkların, gerçek dünyadaki
    // network/disk gecikmeleri (tipik olarak 1-50ms) yanında ne kadar
    // önemsiz kaldığını göstermek. Task.Delay(1) burada "en iyimser"
    // (en hızlı gerçekçi) senaryoyu temsil ediyor; gerçek S3/DB/HTTP
    // çağrıları genelde çok daha yavaştır.
    //
    // NOT: Bu benchmark'lar BenchmarkDotNet'in varsayılan iterasyon
    // sayısıyla uzun sürebilir; gerekirse [SimpleJob(launchCount:1,
    // warmupCount:2, iterationCount:5)] gibi bir job ile sınırlandırın.
    // ========================================================================

    [Benchmark(Description = "[I/O] Pipeline Happy Path (3 Steps, ~1ms each)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> IoBound_Pipeline_HappyPath() {
        var pipeline = new CompensationPipeline<BenchmarkContext>()
            .AddStep(
                name: "IoStep_1",
                execute: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value++; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; })
            .AddStep(
                name: "IoStep_2",
                execute: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value++; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; })
            .AddStep(
                name: "IoStep_3",
                execute: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value++; },
                compensate: (ctx, ct) => { ctx.Value--; return ValueTask.CompletedTask; });

        BenchmarkContext context = new();
        return await pipeline.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "[I/O] Pipeline Faulted & Rollback (3 Steps, ~1ms each)")]
    public async ValueTask<CompensationReport<BenchmarkContext>> IoBound_Pipeline_Faulted() {
        var pipeline = new CompensationPipeline<BenchmarkContext>()
            .AddStep(
                name: "IoStep_1",
                execute: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value++; },
                compensate: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value--; })
            .AddStep(
                name: "IoStep_2",
                execute: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value++; },
                compensate: async (ctx, ct) => { await Task.Delay(1, ct); ctx.Value--; })
            .AddStep(
                name: "IoStep_3_Faulty",
                execute: async (ctx, ct) => { await Task.Delay(1, ct); throw new InvalidOperationException("Simulated I/O failure."); });

        BenchmarkContext context = new();
        return await pipeline.RunAsync(context, CancellationToken.None);
    }

    [Benchmark(Description = "[I/O][Manual] Faulted & Rollback (3 Steps, ~1ms each)")]
    public async Task<BenchmarkContext> IoBound_Manual_Faulted() {
        var context = new BenchmarkContext();
        var rollbackActions = new Stack<Func<BenchmarkContext, CancellationToken, Task>>();

        try {
            await Task.Delay(1);
            context.Value++;
            rollbackActions.Push(async (c, ct) => { await Task.Delay(1, ct); c.Value--; });

            await Task.Delay(1);
            context.Value++;
            rollbackActions.Push(async (c, ct) => { await Task.Delay(1, ct); c.Value--; });

            await Task.Delay(1);
            throw new InvalidOperationException("Simulated I/O failure.");
        }
        catch(InvalidOperationException) {
            while(rollbackActions.Count > 0) {
                await rollbackActions.Pop()(context, CancellationToken.None);
            }
        }

        return context;
    }
}