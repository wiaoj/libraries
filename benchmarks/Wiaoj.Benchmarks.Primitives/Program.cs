using BenchmarkDotNet.Running;
using System;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.Primitives;

internal class Program {
    static void Main(string[] args) {
        // var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        var _ = BenchmarkRunner.Run<BufferAndListBenchmarks>();
        //var _ = BenchmarkRunner.Run<IdGenerationBenchmark>();
        //BenchmarkRunner.Run<PublicIdBenchmark>(); 
        //BenchmarkRunner.Run<ValueListVsListBenchmark>();
        //var _ = BenchmarkRunner.Run<SpanSplitterBenchmark>(); 
        //var _ = BenchmarkRunner.Run<HashingBenchmark>(); 
    
    }
}