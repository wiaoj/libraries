using System;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using IdGen;
using Wiaoj.Primitives.Snowflake;
using Snowflake.Core; // YENİ EKLENDİ

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
[CategoriesColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IdGenerationBenchmark {
    
    // 1. Senin yazdığın
    private readonly SnowflakeGenerator _wiaojGenerator;
    private readonly SnowflakeId _wiaojExistingId;

    // 2. IdGen Kütüphanesi (RobII)
    private readonly IdGenerator _idGenGenerator;
    private readonly long _idGenExistingId;

    // 3. Snowflake.Core (Popüler standart kütüphane) - YENİ
    private readonly IdWorker _snowflakeCoreGenerator;
    private readonly long _snowflakeCoreExistingId;

    // 4. Diğerleri
    private readonly System.Ulid _ulidExistingId;
    private readonly Guid _guidV7ExistingId;

    private byte[] _byteBuffer; 

    public IdGenerationBenchmark() {
        // --- Wiaoj Setup ---
        _wiaojGenerator = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 1 });
        _wiaojExistingId = _wiaojGenerator.NextId();

        // --- IdGen Setup (DÜZELTME YAPILDI) ---
        // IdGen normalde sequence biterse hata fırlatır, bu da benchmark'ı patlatır (NA görünür).
        // SequenceOverflowStrategy.SpinWait diyerek "sıra biterse bekle" diyoruz.
        // Bu sayede senin kütüphanenle adil bir yarışa girecek.
        var structure = new IdStructure(41, 10, 12);
        var options = new IdGeneratorOptions(structure, new DefaultTimeSource(new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc)), SequenceOverflowStrategy.SpinWait);
        _idGenGenerator = new IdGenerator(1, options);
        _idGenExistingId = _idGenGenerator.CreateId();

        // --- Snowflake.Core Setup (YENİ) ---
        // WorkerId: 1, DatacenterId: 1
        _snowflakeCoreGenerator = new IdWorker(1, 1);
        _snowflakeCoreExistingId = _snowflakeCoreGenerator.NextId();

        // --- Others ---
        _ulidExistingId = System.Ulid.NewUlid();
        _guidV7ExistingId = Guid.CreateVersion7();

        _byteBuffer = new byte[16];
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

    // Senin yazdığın Snowflake
    [BenchmarkCategory("Generation"), Benchmark]
    public SnowflakeId Wiaoj_Snowflake_New() => _wiaojGenerator.NextId();

    // IdGen Kütüphanesi (Artık hata vermeyecek, bekleyecek)
    [BenchmarkCategory("Generation"), Benchmark]
    public long External_IdGen_New() => _idGenGenerator.CreateId();

    // Snowflake.Core Kütüphanesi (Yeni Eklenen)
    [BenchmarkCategory("Generation"), Benchmark]
    public long External_SnowflakeCore_New() => _snowflakeCoreGenerator.NextId();


    // =========================================================================
    // KATEGORİ 2: STRING FORMATTING
    // =========================================================================

    [BenchmarkCategory("ToString"), Benchmark]
    public string System_Guid_ToString() => _guidV7ExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string Wiaoj_Snowflake_ToString() => _wiaojExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string External_IdGen_ToString() => _idGenExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string External_SnowflakeCore_ToString() => _snowflakeCoreExistingId.ToString();

    [BenchmarkCategory("ToString"), Benchmark]
    public string External_Ulid_ToString() => _ulidExistingId.ToString();

    // =========================================================================
    // KATEGORİ 3: BINARY (VERİTABANI YAZMA)
    // =========================================================================

    [BenchmarkCategory("Binary"), Benchmark]
    public bool Wiaoj_Snowflake_TryWrite() {
        return _wiaojExistingId.TryWriteBytes(_byteBuffer);
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