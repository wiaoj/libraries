using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System;
using Wiaoj.Results;

namespace Wiaoj.Benchmarks.Results;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ResultCreationBenchmarks {

    private static readonly Error CachedError = Error.Validation("User.Invalid", "Invalid user data.");

    [Benchmark(Baseline = true)]
    public Result<int> Create_SuccessResult() {
        return Result.Success(42);
    }

    [Benchmark]
    public Result<int> Create_FailureResult_CachedError() {
        return Result.Failure<int>(CachedError);
    }

    [Benchmark]
    public Result<int> Create_FailureResult_NewError() {
        return Result.Failure<int>(Error.Validation("User.Invalid", "Invalid user data."));
    }

    [Benchmark]
    public int Classic_ThrowAndCatchException() {
        try {
            throw new InvalidOperationException("Invalid user data.");
        }
        catch(InvalidOperationException) {
            return -1;
        }
    }
}