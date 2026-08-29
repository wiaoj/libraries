using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System.Threading.Tasks;
using Wiaoj.Results;

namespace Wiaoj.Benchmarks.Results;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ResultPipelineBenchmarks {

    private static readonly Error NegativeError = Error.Validation("Number.Negative", "Number must be positive.");
    private static readonly Error TooLargeError = Error.Validation("Number.TooLarge", "Number is too large.");

    [Benchmark(Baseline = true)]
    public int Imperative_IfChain_Success() {
        int val = 10;
        if(val <= 0) return -1;
        val *= 2;
        if(val > 100) return -2;
        return val + 5;
    }

    [Benchmark]
    public Result<int> Functional_ResultChain_Success() {
        return Result.Success(10)
            .Ensure(v => v > 0, NegativeError)
            .Map(v => v * 2)
            .Ensure(v => v <= 100, TooLargeError)
            .Map(v => v + 5);
    }

    [Benchmark]
    public Result<int> Functional_ResultChain_EarlyFailure() {
        return Result.Success(-10)
            .Ensure(v => v > 0, NegativeError)
            .Map(v => v * 2)
            .Ensure(v => v <= 100, TooLargeError)
            .Map(v => v + 5);
    }

    [Benchmark]
    public async Task<Result<int>> Async_Pipeline_Success() {
        return await Task.FromResult(Result.Success(10))
            .EnsureAsync(v => v > 0, NegativeError)
            .MapAsync(v => v * 2)
            .ThenAsync(v => Task.FromResult(Result.Success(v + 5)));
    }

    [Benchmark]
    public async Task<Result<int>> Async_Pipeline_EarlyFailure() {
        return await Task.FromResult(Result.Failure<int>(NegativeError))
            .EnsureAsync(v => v > 0, NegativeError)
            .MapAsync(v => v * 2)
            .ThenAsync(v => Task.FromResult(Result.Success(v + 5)));
    }
}