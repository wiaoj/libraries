using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.Primitives;  
[MemoryDiagnoser]
public class HashingBenchmark {
    private byte[] _data;
    private Secret<byte> _secretData;

    [GlobalSetup]
    public void Setup() {
        this._data = new byte[1024]; // 1 KB 
        RandomNumberGenerator.Fill(this._data);
        this._secretData = Secret.From(this._data);
    }

    [GlobalCleanup]
    public void Cleanup() {
        this._secretData.Dispose();
    }

    [Benchmark(Baseline = true)]
    public byte[] System_SHA256_HashData() {
        return SHA256.HashData(this._data);
    }

    [Benchmark]
    public Sha256Hash Wiaoj_Sha256_Compute_Bytes() {
        return Sha256Hash.Compute(this._data);
    }

    [Benchmark]
    public Sha256Hash Wiaoj_Sha256_Compute_Secret() {
        return Sha256Hash.Compute(this._secretData);
    }
}