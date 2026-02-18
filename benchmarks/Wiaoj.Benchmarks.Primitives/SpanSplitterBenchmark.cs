using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Benchmarks.Primitives;
[MemoryDiagnoser]
[RankColumn]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class SpanSplitterBenchmark {
    // --- Test Verileri ---
    private string? _shortText;
    private string? _longText;

    // Tek karakter ayırıcı
    private const char Separator = ',';

    // Çoklu ayırıcı (SearchValues testi için)
    private const string SeparatorsStr = ",;|";
    private readonly char[] _separatorArray = SeparatorsStr.ToCharArray();
    private SearchValues<char>? _searchValues;

    [GlobalSetup]
    public void Setup() {
        // 1. Kısa Metin (Tipik CSV satırı veya Config değeri)
        this._shortText = "ID,Name,Value,Category,Active,CreatedDate,UpdatedDate,Notes";

        // 2. Uzun Metin (Büyük log satırı veya veri bloğu) - Yaklaşık 10KB
        // Rastgele kelimeler ve aralarına , ; | serpiştiriyoruz
        Random rand = new(42);
        var chars = new char[10_000];
        for(int i = 0; i < chars.Length; i++) {
            if(i % 10 == 0)
                chars[i] = SeparatorsStr[rand.Next(SeparatorsStr.Length)]; // Ayırıcı koy
            else
                chars[i] = (char)rand.Next('a', 'z'); // Harf koy
        }
        this._longText = new string(chars);

        // SearchValues optimizasyonu için cache oluşturma
        this._searchValues = SearchValues.Create(SeparatorsStr);
    }

    // =========================================================
    // SENARYO 1: Tek Karakter ile Split (Kısa Metin)
    // =========================================================

    [Benchmark(Baseline = true, Description = "String.Split (Short)")]
    public int StringSplit_Short() {
        int sum = 0;
        // Klasik yöntem: Array allocation + String allocation
        foreach(var part in this._shortText!.Split(Separator)) {
            sum += part.Length;
        }
        return sum;
    }

    [Benchmark(Description = ".NET Span.Split (Short)")]
    public int DotNetSpanSplit_Short() {
        int sum = 0;
        ReadOnlySpan<char> span = this._shortText.AsSpan();

        // .NET yerleşik Span Split (Range döndürür, biraz daha maliyetli olabilir)
        foreach(var range in span.Split(Separator)) {
            // Range'i kullanarak slice alıyoruz
            sum += span[range].Length;
        }
        return sum;
    }

    [Benchmark(Description = "Wiaoj Splitter (Short)")]
    public int WiaojSplitter_Short() {
        int sum = 0;
        // Senin yazdığın struct
        foreach(var part in this._shortText!.SplitValue(Separator)) {
            sum += part.Length;
        }
        return sum;
    }

    // =========================================================
    // SENARYO 2: Tek Karakter ile Split (Uzun Metin)
    // =========================================================

    [Benchmark(Description = "String.Split (Long)")]
    public int StringSplit_Long() {
        int sum = 0;
        foreach(var part in this._longText!.Split(Separator)) {
            sum += part.Length;
        }
        return sum;
    }

    [Benchmark(Description = "Wiaoj Splitter (Long)")]
    public int WiaojSplitter_Long() {
        int sum = 0;
        foreach(var part in this._longText.AsSpan().SplitValue(Separator)) {
            sum += part.Length;
        }
        return sum;
    }

    // =========================================================
    // SENARYO 3: Çoklu Ayırıcı (Multi Separator - SearchValues)
    // =========================================================

    [Benchmark(Description = "String.Split Any (Multi)")]
    public int StringSplitAny_Multi() {
        int sum = 0;
        // Split(char[]) kullanır
        foreach(var part in this._shortText!.Split(this._separatorArray)) {
            sum += part.Length;
        }
        return sum;
    }

    [Benchmark(Description = "Wiaoj Splitter (SearchValues)")]
    public int WiaojSplitter_SearchValues() {
        int sum = 0;
        // Senin SearchValues optimizasyonlu metodun
        foreach(var part in this._shortText.AsSpan().SplitValue(this._searchValues!)) {
            sum += part.Length;
        }
        return sum;
    }
}