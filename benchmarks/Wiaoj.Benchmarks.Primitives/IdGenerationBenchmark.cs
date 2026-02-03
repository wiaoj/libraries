using System;
using System.Buffers.Binary;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using IdGen;
using Wiaoj.Primitives.Snowflake; // Kendi Snowflake namespace'in
using Wiaoj.Primitives;           // NanoId'nin olduğu yer
using Snowflake.Core;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
[CategoriesColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IdGenerationBenchmark {

    // 1. Wiaoj Primitives
    private readonly SnowflakeGenerator _wiaojSnowflakeGenerator;
    private readonly SnowflakeId _wiaojSnowflakeExistingId;
    private readonly NanoId _wiaojNanoIdExistingId;

    // 2. IdGen Kütüphanesi
    private readonly IdGenerator _idGenGenerator;
    private readonly long _idGenExistingId;

    // 3. Snowflake.Core
    private readonly IdWorker _snowflakeCoreGenerator;
    private readonly long _snowflakeCoreExistingId;

    // 4. Others
    private readonly System.Ulid _ulidExistingId;
    private readonly Guid _guidV7ExistingId;

    private byte[] _byteBuffer;

    public IdGenerationBenchmark() {
        // --- Wiaoj Setup ---
        _wiaojSnowflakeGenerator = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 1 });
        _wiaojSnowflakeExistingId = _wiaojSnowflakeGenerator.NextId();
        _wiaojNanoIdExistingId = NanoId.NewId(); // NanoId Setup

        // --- IdGen Setup ---
        var structure = new IdStructure(41, 10, 12);
        var options = new IdGeneratorOptions(structure, new DefaultTimeSource(new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc)), SequenceOverflowStrategy.SpinWait);
        _idGenGenerator = new IdGenerator(1, options);
        _idGenExistingId = _idGenGenerator.CreateId();

        // --- Snowflake.Core Setup ---
        _snowflakeCoreGenerator = new IdWorker(1, 1);
        _snowflakeCoreExistingId = _snowflakeCoreGenerator.NextId();

        // --- Others ---
        _ulidExistingId = System.Ulid.NewUlid();
        _guidV7ExistingId = Guid.CreateVersion7();

        // Buffer: NanoId (21 char) için en az 21 byte lazım.
        _byteBuffer = new byte[32];
    }

    // =========================================================================
    // KATEGORİ 1: GENERATION (Hız Testi)
    // =========================================================================

    [BenchmarkCategory("Generation"), Benchmark(Baseline = true)]
    public Guid System_Guid_V4_New() => Guid.NewGuid();

    [BenchmarkCategory("Generation"), Benchmark]
    public Guid System_Guid_V7_New() => Guid.CreateVersion7();

    [BenchmarkCategory("Generation"), Benchmark]
    public System.Ulid External_Ulid_New() => System.Ulid.NewUlid();

    [BenchmarkCategory("Generation"), Benchmark]
    public SnowflakeId Wiaoj_Snowflake_New() => _wiaojSnowflakeGenerator.NextId();

    // YENİ: NanoId Üretimi (Secure Random + String Allocation)
    [BenchmarkCategory("Generation"), Benchmark]
    public NanoId Wiaoj_NanoId_New() => NanoId.NewId();

    [BenchmarkCategory("Generation"), Benchmark]
    public long External_IdGen_New() => _idGenGenerator.CreateId();

    [BenchmarkCategory("Generation"), Benchmark]
    public long External_SnowflakeCore_New() => _snowflakeCoreGenerator.NextId();


    // =========================================================================
    // KATEGORİ 2: STRING FORMATTING (Metin Dönüşümü)
    // =========================================================================

    [BenchmarkCategory("ToString"), Benchmark]
    public string System_Guid_ToString() => _guidV7ExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string Wiaoj_Snowflake_ToString() => _wiaojSnowflakeExistingId.ToString();

    // YENİ: NanoId zaten bir string sarmalayıcıdır, maliyeti çok düşüktür.
    [BenchmarkCategory("ToString"), Benchmark]
    public string Wiaoj_NanoId_ToString() => _wiaojNanoIdExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string External_IdGen_ToString() => _idGenExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string External_SnowflakeCore_ToString() => _snowflakeCoreExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string External_Ulid_ToString() => _ulidExistingId.ToString();

    // =========================================================================
    // KATEGORİ 3: BINARY (Serileştirme)
    // =========================================================================

    [BenchmarkCategory("Binary"), Benchmark]
    public bool Wiaoj_Snowflake_TryWrite() {
        return _wiaojSnowflakeExistingId.TryWriteBytes(_byteBuffer);
    }

    // YENİ: NanoId String'ini UTF8 byte olarak yazma maliyeti
    [BenchmarkCategory("Binary"), Benchmark]
    public int Wiaoj_NanoId_Write() {
        return Encoding.UTF8.GetBytes(_wiaojNanoIdExistingId.Value, _byteBuffer);
    }

    [BenchmarkCategory("Binary"), Benchmark]
    public bool External_Ulid_TryWrite() {
        return _ulidExistingId.TryWriteBytes(_byteBuffer);
    }

    [BenchmarkCategory("Binary"), Benchmark]
    public void External_IdGen_Write() {
        BinaryPrimitives.WriteInt64BigEndian(_byteBuffer, _idGenExistingId);
    }

    [BenchmarkCategory("Binary"), Benchmark]
    public void External_SnowflakeCore_Write() {
        BinaryPrimitives.WriteInt64BigEndian(_byteBuffer, _snowflakeCoreExistingId);
    }
}