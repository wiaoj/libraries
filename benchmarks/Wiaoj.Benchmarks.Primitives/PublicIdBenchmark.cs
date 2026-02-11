using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser] // Bellek kullanımını ölçer
[RankColumn]      // Hız sıralaması yapar
public class PublicIdBenchmark {
    private PublicId _snowflakePublicId;
    private PublicId _guidPublicId;
    private string _snowflakePublicString;
    private byte[] _snowflakePublicUtf8Bytes;

    private SnowflakeId _rawSnowflake;
    private Guid _rawGuid;

    private char[] _charBuffer;
    private byte[] _utf8Buffer;

    [GlobalSetup]
    public void Setup() {
        // Konfigürasyon
        PublicId.Configure("Wiaoj-Super-Secret-Key-2025");
        SnowflakeId.Configure(1);

        _rawSnowflake = SnowflakeId.NewId();
        _rawGuid = Guid.NewGuid();

        _snowflakePublicId = new PublicId(_rawSnowflake);
        _guidPublicId = new PublicId(_rawGuid);

        // Parse testleri için önceden encode edilmiş veriler
        _snowflakePublicString = _snowflakePublicId.ToString();
        _snowflakePublicUtf8Bytes = Encoding.UTF8.GetBytes(_snowflakePublicString);

        _charBuffer = new char[64];
        _utf8Buffer = new byte[64];
    }

    // =========================================================================
    // KATEGORİ 1: FORMATTING (ID -> STRING)
    // =========================================================================

    [Benchmark(Baseline = true)]
    public string Raw_Snowflake_ToString() => _rawSnowflake.ToString();

    [Benchmark]
    public string PublicId_Snowflake_ToString() => _snowflakePublicId.ToString();

    [Benchmark]
    public string PublicId_Guid_ToString() => _guidPublicId.ToString();

    /// <summary>
    /// En hızlı senaryo: Sıfır allocation ile doğrudan buffer'a yazma.
    /// </summary>
    [Benchmark]
    public bool PublicId_TryFormat_ZeroAlloc() {
        return _snowflakePublicId.TryFormat(_charBuffer, out _, default, null);
    }

    // =========================================================================
    // KATEGORİ 2: PARSING (STRING -> ID)
    // =========================================================================

    [Benchmark]
    public PublicId PublicId_Parse_String() {
        return PublicId.Parse(_snowflakePublicString);
    }

    /// <summary>
    /// IUtf8SpanParsable sayesinde byte[] -> string dönüşümü yapmadan doğrudan parse.
    /// </summary>
    [Benchmark]
    public PublicId PublicId_Parse_Utf8_ZeroAlloc() {
        return PublicId.Parse(_snowflakePublicUtf8Bytes);
    }

    // =========================================================================
    // KATEGORİ 3: JSON ROUNDTRIP (E2E)
    // =========================================================================

    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly IdContainer _container = new() { Id = SnowflakeId.NewId() };

    [Benchmark]
    public string Json_Serialize_PublicId() {
        return JsonSerializer.Serialize(_container, JsonOptions);
    }

    [Benchmark]
    public IdContainer? Json_Deserialize_PublicId() {
        // Burada kütüphanemiz IUtf8SpanParsable kullandığı için 
        // JSON içindeki ID'yi stringe çevirmeden okuyacak.
        ReadOnlySpan<byte> jsonBytes = "{\"Id\":\"1xgY4h9Kj7g\"}"u8;
        return JsonSerializer.Deserialize<IdContainer>(jsonBytes, JsonOptions);
    }

    public class IdContainer {
        public PublicId Id { get; set; }
    }
}