using System;
using System.Buffers;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using IdGen;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
[CategoriesColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IdGenerationBenchmark {
    
    private readonly SnowflakeGenerator _wiaojGenerator;
    private readonly SnowflakeId _wiaojExistingId;

    private readonly IdGenerator _idGenGenerator;
    private readonly long _idGenExistingId;

    private readonly System.Ulid _ulidExistingId;
    private readonly Guid _guidV7ExistingId;

    // BufferWriter'ı kaldırdım, sadece byte[] kullanacağız (Adil olması için)
    private byte[] _byteBuffer; 

    public IdGenerationBenchmark() {
        _wiaojGenerator = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 1 });
        _wiaojExistingId = _wiaojGenerator.NextId();

        // IdGen'i varsayılan ayarlarla kuruyoruz
        _idGenGenerator = new IdGenerator(1);
        _idGenExistingId = _idGenGenerator.CreateId();

        _ulidExistingId = System.Ulid.NewUlid();
        _guidV7ExistingId = Guid.CreateVersion7();

        _byteBuffer = new byte[16];
    }

    // =========================================================================
    // KATEGORİ 1: GENERATION (Oluşturma)
    // =========================================================================
    
    [BenchmarkCategory("Generation"), Benchmark(Baseline = true)]
    public Guid System_Guid_V4_New() => Guid.NewGuid();

    [BenchmarkCategory("Generation"), Benchmark]
    public Guid System_Guid_V7_New() => Guid.CreateVersion7();

    [BenchmarkCategory("Generation"), Benchmark]
    public SnowflakeId Wiaoj_Snowflake_New() => _wiaojGenerator.NextId();

    [BenchmarkCategory("Generation"), Benchmark]
    public System.Ulid External_Ulid_New() => System.Ulid.NewUlid();

    // IdGen yine patlayabilir çünkü yük altında sequence bitiyor.
    // Ama testte dursun, patlaması bile bir sonuçtur.
    [BenchmarkCategory("Generation"), Benchmark]
    public long External_IdGen_New() => _idGenGenerator.CreateId();

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
    public string External_Ulid_ToString() => _ulidExistingId.ToString();

    // =========================================================================
    // KATEGORİ 3: BINARY (VERİTABANI YAZMA) - İŞTE BURASI DÜZELDİ
    // =========================================================================

    [BenchmarkCategory("Binary"), Benchmark]
    public bool System_Guid_TryWrite() {
        return _guidV7ExistingId.TryWriteBytes(_byteBuffer);
    }

    [BenchmarkCategory("Binary"), Benchmark]
    public bool Wiaoj_Snowflake_TryWrite() {
        // DÜZELTME: Artık IBufferWriter değil, doğrudan Span'a yazıyoruz.
        // Guid ve Ulid ile aynı şartlarda yarışacak.
        return _wiaojExistingId.TryWriteBytes(_byteBuffer);
    }

    [BenchmarkCategory("Binary"), Benchmark]
    public bool External_Ulid_TryWrite() {
        return _ulidExistingId.TryWriteBytes(_byteBuffer);
    }

    [BenchmarkCategory("Binary"), Benchmark]
    public void External_IdGen_Write() {
        // IdGen bir struct olmadığı için manuel yazıyoruz
        BinaryPrimitives.WriteInt64BigEndian(_byteBuffer, _idGenExistingId);
    }
}