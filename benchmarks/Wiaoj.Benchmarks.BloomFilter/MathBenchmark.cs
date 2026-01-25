using BenchmarkDotNet.Attributes;
using System;

namespace Wiaoj.Benchmarks.BloomFilter;
public class MathBenchmark {
    private ulong[] _hashes;
    private readonly ulong _size = 10_000_000;

    [GlobalSetup]
    public void Setup() {
        this._hashes = new ulong[1_000_000];
        Random rnd = new(42);
        for(int i = 0; i < 1_000_000; i++)
            this._hashes[i] = (ulong)rnd.NextInt64();
    }

    [Benchmark(Baseline = true)]
    public ulong StandardModulo() {
        ulong sum = 0;
        for(int i = 0; i < this._hashes.Length; i++)
            sum += this._hashes[i] % this._size; // Klasik yöntem
        return sum;
    }

    [Benchmark]
    public ulong FastRange() {
        ulong sum = 0;
        for(int i = 0; i < this._hashes.Length; i++)
            sum += (ulong)(((UInt128)this._hashes[i] * this._size) >> 64); // Senin yöntemin
        return sum;
    }
}