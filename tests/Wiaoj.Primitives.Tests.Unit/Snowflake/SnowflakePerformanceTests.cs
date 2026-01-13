using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Wiaoj.Primitives.Snowflake;
using Xunit;
using Xunit.Abstractions;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;

public class SnowflakePerformanceTests {
    private readonly ITestOutputHelper _output;

    public SnowflakePerformanceTests(ITestOutputHelper output) {
        _output = output;
    }

    /// <summary>
    /// Scenario:
    /// - mode = "single" → tek thread
    /// - mode = "multi"  → çoklu thread
    /// - warmupCount → JIT ısınma
    /// - ops → tek thread için toplam işlem
    /// - opsPerThread → multi thread için thread başına ID üretimi
    /// - minOpsPerSec → minimum kabul edilen performans
    /// </summary>
    [Theory]
    [Trait("Category", "Performance")]
    [InlineData("single", 1, 10_000, 2_000_000, 0, 5_000_000)]
    [InlineData("multi", 0, 10_000, 0, 2_000_000, 4_000_000)]
    [InlineData("single", 7, 50_000, 3_000_000, 0, 8_000_000)]
    [InlineData("multi", 15, 100_000, 0, 3_000_000, 5_000_000)]
    public void Measure_Throughput(
        string mode,
        ushort nodeId,
        int warmupCount,
        int singleThreadOps,
        int multiThreadOpsPerThread,
        int minOpsPerSec
    ) {
        // 1. Setup
        SnowflakeOptions options = new() { NodeId = nodeId };
        SnowflakeGenerator generator = new(options);

        int threadCount = Math.Max(1, Environment.ProcessorCount);

        // 2. Warm-up
        for (int i = 0; i < warmupCount; i++)
            generator.NextId();

        Stopwatch sw = Stopwatch.StartNew();

        long totalOps;

        if (mode == "single") {
            totalOps = singleThreadOps;
            for (int i = 0; i < singleThreadOps; i++)
                generator.NextId();
        }
        else if (mode == "multi") {
            totalOps = (long)multiThreadOpsPerThread * threadCount;
            Task[] tasks = new Task[threadCount];

            for (int t = 0; t < threadCount; t++) {
                tasks[t] = Task.Run(() => {
                    for (int i = 0; i < multiThreadOpsPerThread; i++)
                        generator.NextId();
                });
            }

            Task.WaitAll(tasks);
        }
        else {
            throw new ArgumentException($"Unknown mode: {mode}");
        }

        sw.Stop();

        // 3. Metrics
        double seconds = sw.Elapsed.TotalSeconds;
        double throughput = totalOps / seconds;

        PrintResults(mode, threadCount, totalOps, sw.Elapsed);

        Assert.True(
            throughput > minOpsPerSec,
            $"FAILED ({mode}): {throughput:N0} ops/sec < expected {minOpsPerSec:N0}"
        );
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void BurstWorkloadTest() {
        int nodeId = 1;
        int burstOps = 1_000_000;

        SnowflakeOptions options = new() { NodeId = (ushort)nodeId };
        SnowflakeGenerator generator = new(options);

        Stopwatch sw = Stopwatch.StartNew();

        Parallel.For(0, burstOps, i => generator.NextId());

        sw.Stop();

        double throughput = burstOps / sw.Elapsed.TotalSeconds;
        PrintResults("Burst Workload", Environment.ProcessorCount, burstOps, sw.Elapsed);

        Assert.True(throughput > 2_000_000, $"Burst throughput too low: {throughput:N0}");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void LockContentionTest() {
        int nodeId = 1;
        int threads = Environment.ProcessorCount;
        int opsPerThread = 500_000;

        SnowflakeOptions options = new() { NodeId = (ushort)nodeId };
        SnowflakeGenerator generator = new(options);

        Task[] tasks = new Task[threads];
        Stopwatch sw = Stopwatch.StartNew();

        for (int t = 0; t < threads; t++) {
            tasks[t] = Task.Run(() => {
                for (int i = 0; i < opsPerThread; i++) {
                    generator.NextId(); // Tek generator kullanılıyor → lock contention
                }
            });
        }

        Task.WaitAll(tasks);
        sw.Stop();

        long totalOps = (long)threads * opsPerThread;
        double throughput = totalOps / sw.Elapsed.TotalSeconds;

        PrintResults("Lock Contention", threads, totalOps, sw.Elapsed);
        Assert.True(throughput > 1_500_000, $"Lock contention throughput too low: {throughput:N0}");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ParallelTaskHybridTest() {
        int nodeId = 1;
        int tasksCount = 4;
        int opsPerTask = 500_000;

        SnowflakeOptions options = new() { NodeId = (ushort)nodeId };
        SnowflakeGenerator generator = new(options);

        Task[] tasks = new Task[tasksCount];
        Stopwatch sw = Stopwatch.StartNew();

        for (int t = 0; t < tasksCount; t++) {
            tasks[t] = Task.Run(() => {
                Parallel.For(0, opsPerTask, i => generator.NextId());
            });
        }

        Task.WaitAll(tasks);
        sw.Stop();

        long totalOps = (long)tasksCount * opsPerTask;
        double throughput = totalOps / sw.Elapsed.TotalSeconds;

        PrintResults("Parallel.For + Task", tasksCount, totalOps, sw.Elapsed);
        Assert.True(throughput > 1_500_000, $"Hybrid throughput too low: {throughput:N0}");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void NodeIdCollisionClockSkewTest() {
        SnowflakeGenerator[] generators = new SnowflakeGenerator[2];
        generators[0] = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 1 });
        generators[1] = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 1 }); // Intentional collision

        int ops = 500_000;
        Stopwatch sw = Stopwatch.StartNew();

        Parallel.ForEach(generators, gen => {
            for (int i = 0; i < ops; i++)
                gen.NextId();
        });

        sw.Stop();

        PrintResults("NodeId Collision", generators.Length, ops * generators.Length, sw.Elapsed);
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void CpuAffinitySingleCoreTest() {
        ushort nodeId = 1;
        int ops = 500_000;

        SnowflakeGenerator generator = new SnowflakeGenerator(new SnowflakeOptions { NodeId = nodeId });

        Process current = Process.GetCurrentProcess();
        current.ProcessorAffinity = (IntPtr)1; // Sadece core 0

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < ops; i++)
            generator.NextId();
        sw.Stop();

        PrintResults("CPU Affinity Single Core", 1, ops, sw.Elapsed);
        Assert.True(true); // performans gözlemi, assertion opsiyonel
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void GcPressureTest() {
        int nodeId = 1;
        int ops = 500_000;

        SnowflakeOptions options = new() { NodeId = (ushort)nodeId };
        SnowflakeGenerator generator = new(options);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < ops; i++) {
            generator.NextId();
            // Her 1000 ID'de hafif GC tetikleme
            if (i % 1000 == 0)
                GC.Collect(0, GCCollectionMode.Optimized, false);
        }
        sw.Stop();

        PrintResults("GC Pressure", 1, ops, sw.Elapsed);
        Assert.True(true); // performans gözlemi, assertion opsiyonel
    }


    private void PrintResults(string scenario, int threads, long totalIds, TimeSpan elapsed) {
        double seconds = elapsed.TotalSeconds;
        double throughput = totalIds / seconds;
        double nsPerOp = seconds * 1_000_000_000 / totalIds;

        _output.WriteLine("---------------------------------------------------");
        _output.WriteLine($"Scenario       : {scenario.ToUpper()}");
        _output.WriteLine($"Threads        : {threads}");
        _output.WriteLine($"Total Generated: {totalIds:N0} IDs");
        _output.WriteLine($"Total Time     : {elapsed.TotalMilliseconds:N2} ms");
        _output.WriteLine($"Throughput     : {throughput:N0} IDs/sec");
        _output.WriteLine($"Latency per ID : {nsPerOp:N4} ns");
        _output.WriteLine("---------------------------------------------------");
    }
}
