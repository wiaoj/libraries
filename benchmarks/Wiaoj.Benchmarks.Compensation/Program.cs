using BenchmarkDotNet.Running;
using Wiaoj.Benchmarks.Compensation;

BenchmarkRunner.Run<PipelineExecutionBenchmarks>();
BenchmarkRunner.Run<AllocationSourceBenchmarks>();
BenchmarkRunner.Run<AsyncAllocationSourceBenchmarks>();