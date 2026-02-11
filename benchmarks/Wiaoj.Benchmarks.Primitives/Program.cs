using BenchmarkDotNet.Running;

namespace Wiaoj.Benchmarks.Primitives;

internal class Program {
    static void Main(string[] args) {
        // var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        //var _ = BenchmarkRunner.Run<IdGenerationBenchmark>();   
        BenchmarkRunner.Run<PublicIdBenchmark>();
    }
}