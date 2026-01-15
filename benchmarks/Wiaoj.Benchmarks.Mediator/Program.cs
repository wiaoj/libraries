using BenchmarkDotNet.Running;

namespace Wiaoj.Benchmarks.Mediator;

internal class Program {
    static void Main(string[] args) {
        var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}
