using BenchmarkDotNet.Attributes;
using System.Threading.Tasks;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Internal.Memory;

namespace Wiaoj.Benchmarks.DistributedCounter;
[MemoryDiagnoser]
public class LibraryLogicBenchmark {
    private IDistributedCounter _buffered;
    private IDistributedCounter _immediate;

    [GlobalSetup]
    public void Setup() {
        // Storage: In-Memory (Redis gecikmesi yok, saf kod performansı)
        InMemoryCounterStorage storage = new Wiaoj.DistributedCounter.Internal.Memory.InMemoryCounterStorage();

        // KeyBuilder
        DefaultCounterKeyBuilder keyBuilder = new Wiaoj.DistributedCounter.Internal.DefaultCounterKeyBuilder();

        // 1. Buffered Instance (Manuel oluşturuyoruz)
        this._buffered = new Wiaoj.DistributedCounter.Internal.BufferedDistributedCounter(
            "bench:buf",
            storage
        );

        // 2. Immediate Instance (Manuel oluşturuyoruz)
        this._immediate = new Wiaoj.DistributedCounter.Internal.ImmediateDistributedCounter(
            "bench:imm",
            storage
        );
    }

    [Benchmark]
    public async Task Buffered_Increment() {
        // Sadece Interlocked.Add + Struct allocation
        await this._buffered.IncrementAsync();
    }

    [Benchmark]
    public async Task Immediate_Increment() {
        // Dictionary Lookup + Interlocked (Storage içinde)
        // Buffered'dan yavaş olmalı ama çok değil (çünkü Redis yok, Memory var)
        await this._immediate.IncrementAsync();
    }
}