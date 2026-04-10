using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq; 
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Benchmarks.Primitives;
// MemoryDiagnoser: Bize "Allocated" (Ayrılan Bellek) ve "Gen 0/1/2" GC raporlarını verir.
[MemoryDiagnoser]
public class BufferAndListBenchmarks {
    // İki senaryo test edeceğiz:
    // 100  -> Stack'e sığan küçük veri (Sıfır Allocation bekliyoruz)
    // 5000 -> Stack'i aşıp ArrayPool'a taşan büyük veri
    [Params(100, 5000, 10000)]
    public int Size { get; set; }

    // ----- VALUE BUFFER vs ARRAY TESTLERİ -----

    [Benchmark(Baseline = true)]
    public int StandardArray() {
        // Standart .NET Dizisi (Heap üzerinde allocation yapar)
        int[] array = new int[this.Size];
        for(int i = 0; i < this.Size; i++) {
            array[i] = i;
        }

        int sum = 0;
        for(int i = 0; i < this.Size; i++) sum += array[i];
        return sum;
    }

    [Benchmark]
    public int CustomValueBuffer() {
        // Senin yazdığın ValueBuffer
        // 256 elemana kadar stack kullanır, aşarsa ArrayPool'dan kiralar
        Span<int> stackSpace = stackalloc int[256];
        using ValueBuffer<int> buffer = new(this.Size, stackSpace);

        for(int i = 0; i < this.Size; i++) {
            buffer[i] = i;
        }

        int sum = 0;
        for(int i = 0; i < this.Size; i++) sum += buffer[i];
        return sum;
    }

    // ----- VALUE LIST vs LIST<T> TESTLERİ -----

    [Benchmark]
    public int StandardList() {
        // Standart List<T>. Kapasite aşımında sürekli yeni dizi oluşturup eskiyi çöpe atar.
        List<int> list = [];
        for(int i = 0; i < this.Size; i++) {
            list.Add(i);
        }

        int sum = 0;
        foreach(var item in list) sum += item;
        return sum;
    }

    [Benchmark]
    public int CustomValueList() {
        // Senin yazdığın ValueList
        Span<int> stackSpace = stackalloc int[256];
        ValueList<int> list = new(stackSpace);

        for(int i = 0; i < this.Size; i++) {
            list.Add(i);
        }

        int sum = 0;
        foreach(var item in list) sum += item;

        list.Dispose(); // Havuza iade
        return sum;
    }

    // ----- LINQ vs YAZDIĞIN SELECTLAZY TESTLERİ -----

    [Benchmark]
    public int StandardList_LINQ() {
        List<int> list = new(this.Size);
        for(int i = 0; i < this.Size; i++) list.Add(i);

        // Standart LINQ Select işlemi
        int sum = 0;
        foreach(var item in list.Select(x => x * 2)) {
            sum += item;
        }
        return sum;
    }

    [Benchmark]
    public int CustomValueList_SelectLazy() {
        Span<int> stackSpace = stackalloc int[256];
        ValueList<int> list = new(stackSpace);
        for(int i = 0; i < this.Size; i++) list.Add(i);

        // Senin yazdığın sıfır-allocation SelectLazy işlemi
        int sum = 0;
        foreach(var item in list.SelectLazy(x => x * 2)) {
            sum += item;
        }

        list.Dispose();
        return sum;
    }
} 