using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.BloomFilter;

[MemoryDiagnoser] // RAM kullanımını gösterir
[HtmlExporter]   // Sonuçları HTML olarak raporlar
public class FilterPerformanceBenchmark {
    private InMemoryBloomFilter _simdFilter;
    private InMemoryBloomFilter _noSimdFilter;
    private byte[][] _testData;

    [Params(100_000)] // 100 bin öğe üzerinde test et
    public int Count;

    [GlobalSetup]
    public void Setup() {
        var config = new BloomFilterConfiguration(
            FilterName.Parse("bench"), Count, Percentage.FromDouble(0.01));

        // SIMD Aktif
        var optSimd = new BloomFilterOptions();
        optSimd.Performance.EnableSimd = true;
        _simdFilter = new InMemoryBloomFilter(config, null, NullLogger.Instance, optSimd, TimeProvider.System);

        // SIMD Kapalı
        var optNoSimd = new BloomFilterOptions();
        optNoSimd.Performance.EnableSimd = false;
        _noSimdFilter = new InMemoryBloomFilter(config, null, NullLogger.Instance, optNoSimd, TimeProvider.System);

        // Test verisi oluştur
        _testData = new byte[Count][];
        for(int i = 0; i < Count; i++) {
            _testData[i] = Guid.NewGuid().ToByteArray();
        }
    }

    [Benchmark]
    public void Add_SIMD() {
        for(int i = 0; i < _testData.Length; i++)
            _simdFilter.Add(_testData[i]);
    }

    [Benchmark]
    public void Add_NoSIMD() {
        for(int i = 0; i < _testData.Length; i++)
            _noSimdFilter.Add(_testData[i]);
    }

    [Benchmark]
    public void Contains_SIMD() {
        for(int i = 0; i < _testData.Length; i++)
            _simdFilter.Contains(_testData[i]);
    }

    [Benchmark]
    public void Contains_NoSIMD() {
        for(int i = 0; i < _testData.Length; i++)
            _noSimdFilter.Contains(_testData[i]);
    }
}