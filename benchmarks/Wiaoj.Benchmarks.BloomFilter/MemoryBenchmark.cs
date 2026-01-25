using BenchmarkDotNet.Attributes;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.Benchmarks.BloomFilter;

[MemoryDiagnoser]
public class MemoryBenchmark {
    [Params(1_000_000, 10_000_000)] // 1M ve 10M bitlik diziler
    public int BitSize;

    [Benchmark(Baseline = true)]
    public void Standard_New_Array() {
        // Havuz kullanmadan her seferinde yeni dizi oluşturmak
        ulong[] array = new ulong[(this.BitSize + 63) / 64];
        array[0] = 1;
    }

    [Benchmark]
    public void Wiaoj_PooledArray() {
        // Senin pooling mantığın
        using PooledBitArray pooled = new(this.BitSize);
        pooled.Set(0);
    }
}