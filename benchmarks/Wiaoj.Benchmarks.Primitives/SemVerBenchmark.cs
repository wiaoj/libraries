using System;
using BenchmarkDotNet.Attributes;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
public class SemVerBenchmark {
    private const string SimpleVersion = "1.2.3";
    private const string ComplexVersion = "1.2.3-beta.1+build.123";

    private readonly SemVer _v1 = new(1, 0, 0);
    private readonly SemVer _v2 = new(1, 0, 1);
    private char[] _charBuffer;

    [GlobalSetup]
    public void Setup() {
        _charBuffer = new char[64];
    }

    // --- PARSING ---

    [Benchmark(Baseline = true)]
    public Version System_Version_Parse() {
        return Version.Parse(SimpleVersion);
    }

    [Benchmark]
    public SemVer Wiaoj_SemVer_Parse_Simple() {
        // Custom int parser etkisi burada görülmeli
        return SemVer.Parse(SimpleVersion);
    }

    [Benchmark]
    public SemVer Wiaoj_SemVer_Parse_Complex() {
        return SemVer.Parse(ComplexVersion);
    }

    // --- FORMATTING ---

    [Benchmark]
    public string Wiaoj_SemVer_ToString() {
        return _v1.ToString();
    }

    [Benchmark]
    public bool Wiaoj_SemVer_TryFormat() {
        // Zero Allocation Formatting
        return _v1.TryFormat(_charBuffer, out _, "G", null);
    }

    // --- COMPARISON ---

    [Benchmark]
    public bool Wiaoj_SemVer_Compare() {
        // 'in' parametresi sayesinde struct kopyalama maliyeti azalmalı
        return _v1 < _v2;
    }
}