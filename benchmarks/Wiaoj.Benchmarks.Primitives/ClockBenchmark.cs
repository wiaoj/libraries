using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
public class ClockBenchmark {
    [Benchmark]
    public long Stopwatch_GetTimestamp() {
        return Stopwatch.GetTimestamp();
    }

    [Benchmark]
    public DateTime DateTime_UtcNow() {
        return DateTime.UtcNow;
    }
}