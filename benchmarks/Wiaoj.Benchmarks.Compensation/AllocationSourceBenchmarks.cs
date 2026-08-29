using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Wiaoj.Benchmarks.Compensation;

// ============================================================================
// AMAÇ: "680B nereden geliyor?" sorusuna tahminle değil, ölçerek cevap vermek.
// Her olası allocation kaynağını izole ederek ayrı ayrı ölçüyoruz.
// ============================================================================
[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class AllocationSourceBenchmarks {

    // ------------------------------------------------------------------
    // 1. Baseline: hiçbir şey yok
    // ------------------------------------------------------------------
    [Benchmark(Baseline = true, Description = "Baseline (no-op)")]
    public int Baseline() {
        return 42;
    }

    // ------------------------------------------------------------------
    // 2. Sadece TEK Stopwatch
    // ------------------------------------------------------------------
    [Benchmark(Description = "1x Stopwatch.StartNew()")]
    public TimeSpan OneStopwatch() {
        var sw = Stopwatch.StartNew();
        sw.Stop();
        return sw.Elapsed;
    }

    // ------------------------------------------------------------------
    // 3. İKİ Stopwatch (pipeline'daki gerçek kullanım: executionSw + rollbackSw)
    // ------------------------------------------------------------------
    [Benchmark(Description = "2x Stopwatch.StartNew()")]
    public TimeSpan TwoStopwatches() {
        var sw1 = Stopwatch.StartNew();
        sw1.Stop();
        var sw2 = Stopwatch.StartNew();
        sw2.Stop();
        return sw1.Elapsed + sw2.Elapsed;
    }

    // ------------------------------------------------------------------
    // 4. Stopwatch'un ALLOCATION-SIZ alternatifi (.NET 7+ statik API)
    //    Bu, Stopwatch nesnesi hiç oluşturmadan aynı zaman ölçümünü sağlar.
    // ------------------------------------------------------------------
    [Benchmark(Description = "2x Stopwatch.GetTimestamp() (alloc-free)")]
    public TimeSpan TwoTimestamps() {
        long start1 = Stopwatch.GetTimestamp();
        TimeSpan elapsed1 = Stopwatch.GetElapsedTime(start1);

        long start2 = Stopwatch.GetTimestamp();
        TimeSpan elapsed2 = Stopwatch.GetElapsedTime(start2);

        return elapsed1 + elapsed2;
    }

    // ------------------------------------------------------------------
    // 5. Sadece CancellationTokenSource (timeout'lu)
    // ------------------------------------------------------------------
    [Benchmark(Description = "1x CancellationTokenSource(timeout)")]
    public bool OneCts() {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return cts.IsCancellationRequested;
    }

    // ------------------------------------------------------------------
    // 6. Sadece List<T> (boş, sonra 0 eleman kalıyor - happy path senaryosu)
    // ------------------------------------------------------------------
    [Benchmark(Description = "Empty List<T>()")]
    public int EmptyList() {
        var list = new List<int>();
        return list.Count;
    }

    // ------------------------------------------------------------------
    // 7. HEPSİ BİRDEN: CompensateAsync'in gerçek profilini taklit eden kombinasyon
    //    (2x Stopwatch + 1x CTS + 1x List) — pipeline'daki gerçek desenle
    //    birebir aynı allocation setini izole şekilde ölçüyoruz.
    // ------------------------------------------------------------------
    [Benchmark(Description = "Combined: 2x Stopwatch + CTS + List (mevcut desen)")]
    public int Combined_Current() {
        var executionSw = Stopwatch.StartNew();
        executionSw.Stop();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var rollbackSw = Stopwatch.StartNew();
        rollbackSw.Stop();
        var errors = new List<int>();

        return errors.Count + (cts.IsCancellationRequested ? 1 : 0);
    }

    // ------------------------------------------------------------------
    // 8. HEPSİ BİRDEN ama Stopwatch yerine GetTimestamp() kullanılan versiyon
    //    (önerilen optimizasyon) — CTS ve List aynı kalıyor, sadece
    //    Stopwatch'un alloc'u kaldırılıyor. Fark buradan net görülür.
    // ------------------------------------------------------------------
    [Benchmark(Description = "Combined: GetTimestamp() + CTS + List (önerilen)")]
    public int Combined_Optimized() {
        long executionStart = Stopwatch.GetTimestamp();
        TimeSpan _ = Stopwatch.GetElapsedTime(executionStart);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        long rollbackStart = Stopwatch.GetTimestamp();
        TimeSpan __ = Stopwatch.GetElapsedTime(rollbackStart);
        var errors = new List<int>();

        return errors.Count + (cts.IsCancellationRequested ? 1 : 0);
    }
}