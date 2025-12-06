using System;
using System.Buffers;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class EncodingBenchmark {
    private byte[] _data;
    private string _base64String;
    private string _hexString;

    // Primitive Tiplerin Instance'ları (WriteTo testi için)
    private HexString _hexStruct;
    private Base64String _base64Struct;
    private Base32String _base32Struct;

    // BufferWriter testi için
    private ArrayBufferWriter<byte> _bufferWriter;

    [GlobalSetup]
    public void Setup() {
        _data = new byte[32]; // 32 bytes (Standard for hashes/keys)
        RandomNumberGenerator.Fill(_data);

        _base64String = Convert.ToBase64String(_data);
        _hexString = Convert.ToHexString(_data).ToLowerInvariant();

        _hexStruct = HexString.FromBytes(_data);
        _base64Struct = Base64String.FromBytes(_data);
        _base32Struct = Base32String.FromBytes(_data);

        _bufferWriter = new ArrayBufferWriter<byte>(1024);
    }

    // --- BASE64 ---

    [Benchmark(Baseline = true)]
    public string Base64_System_Convert() {
        return Convert.ToBase64String(_data);
    }

    [Benchmark]
    public Base64String Base64_FromBytes() {
        // [SkipLocalsInit] etkisi burada görülecek
        return Base64String.FromBytes(_data);
    }

    [Benchmark]
    public Base64String Base64_Parse() {
        // Validation performance
        return Base64String.Parse(_base64String);
    }

    [Benchmark]
    public void Base64_WriteTo_Buffer() {
        // IBufferWriter (Zero Allocation) testi
        _bufferWriter.Clear();
        _base64Struct.WriteTo(_bufferWriter);
    }

    // --- HEX ---

    [Benchmark]
    public string Hex_System_Convert() {
        return Convert.ToHexString(_data);
    }

    [Benchmark]
    public HexString Hex_FromBytes() {
        // SIMD + SkipLocalsInit + string.Create optimizasyonu
        return HexString.FromBytes(_data);
    }

    [Benchmark]
    public HexString Hex_Parse() {
        return HexString.Parse(_hexString);
    }

    [Benchmark]
    public void Hex_WriteTo_Buffer() {
        // IBufferWriter (Zero Allocation) testi
        _bufferWriter.Clear();
        _hexStruct.WriteTo(_bufferWriter);
    }

    // --- BASE32 ---

    [Benchmark]
    public Base32String Base32_FromBytes() {
        // Table lookup array optimizasyonu + SkipLocalsInit
        return Base32String.FromBytes(_data);
    }

    [Benchmark]
    public byte[] Base32_ToBytes() {
        // Decode performansı (Unsafe accessor)
        return _base32Struct.ToBytes();
    }

    [Benchmark]
    public void Base32_WriteTo_Buffer() {
        // IBufferWriter (Zero Allocation) testi
        _bufferWriter.Clear();
        _base32Struct.WriteTo(_bufferWriter);
    }
}