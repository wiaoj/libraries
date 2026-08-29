using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System.Collections.Generic;
using System.Linq;
using Wiaoj.Results;

namespace Wiaoj.Benchmarks.Results;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ResultCollectionBenchmarks {

    private Result<int>[] _allSuccessItems = null!;
    private Result<int>[] _mixedItems = null!;

    [GlobalSetup]
    public void Setup() {
        _allSuccessItems = Enumerable.Range(1, 100)
            .Select(i => Result.Success(i))
            .ToArray();

        _mixedItems = Enumerable.Range(1, 100)
            .Select(i => i % 2 == 0 ? Result.Success(i) : Result.Failure<int>(Error.Failure($"Err.{i}", "Error")))
            .ToArray();
    }

    [Benchmark]
    public Result<IReadOnlyList<int>> Combine_AllSuccess_100Items() {
        return _allSuccessItems.Combine();
    }

    [Benchmark]
    public Result<IReadOnlyList<int>> Combine_Mixed_100Items() {
        return _mixedItems.Combine();
    }

    [Benchmark]
    public (IReadOnlyList<int> Successes, IReadOnlyList<Error> Failures) Partition_Mixed_100Items() {
        return _mixedItems.Partition();
    }

    [Benchmark]
    public Result<Success> All_ParamsSpan_Success() {
        return Result.All(
            Result.Success(),
            Result.Success(),
            Result.Success(),
            Result.Success()
        );
    }
}