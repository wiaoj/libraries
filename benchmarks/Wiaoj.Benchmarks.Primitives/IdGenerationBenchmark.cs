using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
public class IdGenerationBenchmark {
    private readonly SnowflakeGenerator _generator;
    private readonly SnowflakeId _existingId;
    private ArrayBufferWriter<byte> _bufferWriter;

    public IdGenerationBenchmark() {
        _generator = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 1 });
        _existingId = _generator.NextId();
        _bufferWriter = new ArrayBufferWriter<byte>(16);
    }

    [Benchmark(Baseline = true)]
    public Guid System_Guid_NewGuid() {
        return Guid.NewGuid();
    }

    [Benchmark]
    public SnowflakeId Snowflake_NewId() {
        return _generator.NextId();
    }

    // --- FORMATTING COMPARISON ---

    [Benchmark]
    public string System_Guid_ToString() {
        return Guid.NewGuid().ToString();
    }

    [Benchmark]
    public string Snowflake_ToString() {
        return _existingId.ToString();
    }

    [Benchmark]
    public void Snowflake_WriteTo_Buffer() {
        // Binary serialization (Zero Allocation)
        _bufferWriter.Clear();
        _existingId.WriteTo(_bufferWriter);
    }
}