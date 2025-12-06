using System;
using BenchmarkDotNet.Attributes;
using Wiaoj.Primitives;

namespace Wiaoj.Benchmarks.Primitives;

[MemoryDiagnoser]
public class SecretBenchmark {
    private byte[] _sourceData;
    private Secret<byte> _secret;
    private int _dummyState = 123;

    [GlobalSetup]
    public void Setup() {
        _sourceData = new byte[32];
        Random.Shared.NextBytes(_sourceData);
        _secret = Secret.From(_sourceData);
    }

    [GlobalCleanup]
    public void Cleanup() {
        _secret.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int ByteArray_DirectAccess() {
        // Güvenli olmayan referans erişimi (En hızlısı ama güvensiz)
        return _sourceData[0] + _sourceData[1];
    }

    [Benchmark]
    public int Secret_Expose_Closure() {
        // ESKİ YÖNTEM: Closure Allocation Yapar!
        // Lambda dışarıdaki değişkeni (_dummyState) yakaladığı için bir class instantiate edilir.
        int result = 0;
        _secret.Expose(span => {
            result = span[0] + _dummyState;
        });
        return result;
    }

    [Benchmark]
    public int Secret_Expose_State_ZeroAlloc() {
        // YENİ OPTİMİZASYON: Zero Allocation
        // State parametresi ile static lambda kullanırız.
        return _secret.Expose(_dummyState, static (state, span) => {
            return span[0] + state;
        });
    }

    [Benchmark]
    public void Secret_Lifecycle_Full() {
        // Create -> Use -> Dispose döngüsü
        // NativeMemory.Alloc ve Free maliyetini ölçer.
        using var s = Secret.From(_sourceData);
        s.Expose(static span => {
            var b = span[0];
        });
    }
}