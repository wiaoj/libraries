using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Wiaoj.Benchmarks.BloomFilter;
internal class Program {
    private static void Main(string[] args) {
        Summary[] _ = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
} 