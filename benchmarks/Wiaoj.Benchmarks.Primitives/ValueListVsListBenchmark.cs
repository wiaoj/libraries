using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Benchmarks.Primitives; 
[MemoryDiagnoser]
[RankColumn]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class ValueListVsListBenchmark {
    // Test edilecek eleman sayıları
    [Params(1_000, 10_000, 100_000)]
    public int Count;

    // Test edilecek struct verisi (24 byte)
    [StructLayout(LayoutKind.Sequential)]
    public struct GameEntity {
        public int Id;
        public float Health;
        public float X, Y, Z;
        public float VelX;
    } 

    private readonly GameEntity[] _sourceData = new GameEntity[100];

    [GlobalSetup]
    public void Setup() {
        for(int i = 0; i < 100; i++) _sourceData[i] = new GameEntity { Id = i, Health = 100f };
    }

    // --- SENARYO 1: Standart List<T> ---
    [Benchmark(Baseline = true, Description = "List<T>.Add")]
    public int StandardList_Add() {
        // Kapasite vermiyoruz ki resize (büyüme) maliyetini de ölçelim
        List<GameEntity> list = [];

        for(int i = 0; i < this.Count; i++) {
            list.Add(new GameEntity { Id = i, Health = 100f });
        }
        return list.Count;
    }

    [Benchmark(Description = "List<T>.AddRange")]
    public int StandardList_AddRange() {
        List<GameEntity> list = []; // Eşitlik için boş başla
        int repeats = Count / _sourceData.Length;

        for(int i = 0; i < repeats; i++) {
            list.AddRange(_sourceData);
        }
        return list.Count;
    }

    // --- SENARYO 2: ValueList<T>.Add ---
    [Benchmark(Description = "ValueList.Add")]
    [SkipLocalsInit] // Stack temizleme maliyetini kaldırır (ekstra hız)
    public int ValueList_Add() {
        // Stack üzerinde başlangıç buffer'ı (Heap allocation yok)
        Span<GameEntity> buffer = stackalloc GameEntity[16];

        // using kullanıyoruz ki ArrayPool'a geri dönsün
        using ValueList<GameEntity> list = new(buffer);

        for(int i = 0; i < this.Count; i++) {
            list.Add(new GameEntity { Id = i, Health = 100f });
        }
        return list.Count;
    }

    // --- SENARYO 3: ValueList<T>.AddByRef (AddAlloc) ---
    // Bu metodun amacı kopyalama maliyetini (struct copy) yok etmektir.
    [Benchmark(Description = "ValueList.AddByRef")]
    [SkipLocalsInit]
    public int ValueList_AddByRef() {
        Span<GameEntity> buffer = stackalloc GameEntity[16];
        using ValueList<GameEntity> list = new(buffer);

        for(int i = 0; i < this.Count; i++) {
            // DOĞRUDAN BELLEĞE YAZMA (Copy Elision)
            list.AddByRef() = new GameEntity { Id = i, Health = 100f };
        }
        return list.Count;
    }

    [Benchmark(Description = "ValueList.AddRange")]
    [SkipLocalsInit]
    public int ValueList_AddRange() {
        Span<GameEntity> buffer = stackalloc GameEntity[16]; 
        using ValueList<GameEntity> list = new(buffer);

        int repeats = Count / _sourceData.Length;
        ReadOnlySpan<GameEntity> source = _sourceData;

        for(int i = 0; i < repeats; i++) {
            list.AddRange(source); 
        }
        return list.Count;
    }
}