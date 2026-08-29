using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Wiaoj.Benchmarks.Compensation;

// ============================================================================
// AMAÇ: Önceki AllocationSourceBenchmarks tamamen SENKRONdu - JIT'in escape
// analysis'i Stopwatch'u "bedava" gösterdi. Ama gerçek RunAsync/CompensateAsync
// ASYNC metodlar (gerçek await içeriyorlar) - bu, derleyicinin bir state
// machine ürettiği ve lokal değişkenlerin (Stopwatch dahil) o state machine'in
// field'larına taşındığı anlamına gelir. Bu test, o farkı gerçekten yakalıyor.
// ============================================================================
[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class AsyncAllocationSourceBenchmarks {

    // Gerçekten asenkron olsun diye Task.Yield() kullanıyoruz - bu, metodun
    // senkron tamamlanmasını engelleyip state machine'in gerçekten devreye
    // girmesini garanti eder (tıpkı gerçek step.ExecuteAsync çağrıları gibi).

    [Benchmark(Baseline = true, Description = "Async no-op (baseline state machine cost)")]
    public async ValueTask<int> AsyncBaseline() {
        await Task.Yield();
        return 42;
    }

    [Benchmark(Description = "Async + 1x Stopwatch (crosses await)")]
    public async ValueTask<TimeSpan> AsyncOneStopwatch() {
        var sw = Stopwatch.StartNew();
        await Task.Yield();
        sw.Stop();
        return sw.Elapsed;
    }

    [Benchmark(Description = "Async + 2x Stopwatch (crosses await)")]
    public async ValueTask<TimeSpan> AsyncTwoStopwatches() {
        var sw1 = Stopwatch.StartNew();
        await Task.Yield();
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        await Task.Yield();
        sw2.Stop();

        return sw1.Elapsed + sw2.Elapsed;
    }

    [Benchmark(Description = "Async + 2x GetTimestamp() (crosses await, alloc-free)")]
    public async ValueTask<TimeSpan> AsyncTwoTimestamps() {
        long start1 = Stopwatch.GetTimestamp();
        await Task.Yield();
        TimeSpan elapsed1 = Stopwatch.GetElapsedTime(start1);

        long start2 = Stopwatch.GetTimestamp();
        await Task.Yield();
        TimeSpan elapsed2 = Stopwatch.GetElapsedTime(start2);

        return elapsed1 + elapsed2;
    }

    [Benchmark(Description = "Async + CTS(timeout) (crosses await)")]
    public async ValueTask<bool> AsyncOneCts() {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.Yield();
        return cts.IsCancellationRequested;
    }

    // ------------------------------------------------------------------
    // Gerçek pipeline desenini birebir taklit eden kombinasyon:
    // 2x Stopwatch + CTS + List, hepsi await sınırlarını geçiyor.
    // ------------------------------------------------------------------
    [Benchmark(Description = "Async Combined: 2x Stopwatch + CTS + List (mevcut)")]
    public async ValueTask<int> AsyncCombined_Current() {
        var executionSw = Stopwatch.StartNew();
        await Task.Yield();
        executionSw.Stop();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var rollbackSw = Stopwatch.StartNew();
        await Task.Yield();
        rollbackSw.Stop();

        var errors = new List<int>();
        return errors.Count + (cts.IsCancellationRequested ? 1 : 0);
    }

    [Benchmark(Description = "Async Combined: GetTimestamp() + CTS + List (önerilen)")]
    public async ValueTask<int> AsyncCombined_Optimized() {
        long executionStart = Stopwatch.GetTimestamp();
        await Task.Yield();
        TimeSpan _ = Stopwatch.GetElapsedTime(executionStart);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        long rollbackStart = Stopwatch.GetTimestamp();
        await Task.Yield();
        TimeSpan __ = Stopwatch.GetElapsedTime(rollbackStart);

        var errors = new List<int>();
        return errors.Count + (cts.IsCancellationRequested ? 1 : 0);
    }
}