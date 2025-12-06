using System;
using BenchmarkDotNet.Attributes;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
public class UrnBenchmark {
    private const string UrnString = "urn:user:123456789";
    private char[] _charBuffer;
    private Urn _urn;

    [GlobalSetup]
    public void Setup() {
        _charBuffer = new char[128];
        _urn = Urn.Create("user", "123456789");
    }

    // --- PARSING ---

    [Benchmark(Baseline = true)]
    public Uri System_Uri_Parse() {
        return new Uri(UrnString);
    }

    [Benchmark]
    public Urn Wiaoj_Urn_Parse() {
        return Urn.Parse(UrnString);
    }

    // --- CREATION ---

    [Benchmark]
    public string String_Format_Create() {
        return string.Format("urn:{0}:{1}", "user", "123456789");
    }

    [Benchmark]
    public Urn Wiaoj_Urn_Create() {
        return Urn.Create("user", "123456789");
    }

    // --- FORMATTING ---

    [Benchmark]
    public bool Wiaoj_Urn_TryFormat() {
        // Zero Allocation Formatting
        return _urn.TryFormat(_charBuffer, out _, [], null);
    }
}