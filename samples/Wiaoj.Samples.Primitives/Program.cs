using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Primitives.Buffers;

//Console.WriteLine("====================================================");
//Console.WriteLine("    HUGE DATA TEST: SpanSplitter vs String.Split    ");
//Console.WriteLine("    (50,000 Segments / 1MB+ String)                 ");
//Console.WriteLine("====================================================");

//// 1. Devasa veriyi hazırla (Yaklaşık 1.2 MB string)
//Console.WriteLine("Preparing huge data...");
//StringBuilder sb = new();
//for(int i = 0; i < 50_000; i++) {
//    sb.Append(i.ToString("D6")); // "000001", "000002" ...
//    if(i < 49_999) sb.Append(',');
//}
//string hugeData = sb.ToString();
//const int Iterations = 500; // Toplamda 25 Milyon segment işlenecek

//Console.WriteLine($"Huge Data Length: {hugeData.Length:N0} chars");
//Console.WriteLine($"Total Segments to process: {(Iterations * 50_000):N0}\n");

//// --- TEST 1: Standard String.Split ---
//GC.Collect();
//GC.WaitForPendingFinalizers();
//GC.Collect();
//long memBeforeSplit = GC.GetTotalAllocatedBytes(true);
//Stopwatch swSplit = Stopwatch.StartNew();

//long totalLength1 = 0;
//for(int i = 0; i < Iterations; i++) {
//    // HER DÖNGÜDE: 1 Dizi + 50.000 yeni string nesnesi! (GC FELAKETİ)
//    string[] parts = hugeData.Split(',');
//    foreach(var p in parts) {
//        totalLength1 += p.Length;
//    }
//}

//swSplit.Stop();
//long memAfterSplit = GC.GetTotalAllocatedBytes(true);

//// --- TEST 2: Wiaoj SpanSplitter ---
//GC.Collect();
//GC.WaitForPendingFinalizers();
//GC.Collect();
//long memBeforeSpan = GC.GetTotalAllocatedBytes(true);
//Stopwatch swSpan = Stopwatch.StartNew();

//long totalLength2 = 0;
//// SearchValues'ı bir kere oluştur (Best Practice)
//SearchValues<char> sv = SearchValues.Create([',']);

//for(int i = 0; i < Iterations; i++) {
//    // 0 ALLOCATION: Sadece pointer kaydırarak ilerler
//    foreach(var part in hugeData.AsSpan().SplitValue(sv)) {
//        totalLength2 += part.Length;
//    }
//}

//swSpan.Stop();
//long memAfterSpan = GC.GetTotalAllocatedBytes(true);

//// --- RAPOR ---
//Console.WriteLine($"\n[Standard String.Split]");
//Console.WriteLine($"  Time      : {swSplit.ElapsedMilliseconds} ms");
//Console.WriteLine($"  Allocated : {FormatBytes(memAfterSplit - memBeforeSplit)}");

//Console.WriteLine($"\n[Wiaoj SpanSplitter]");
//Console.WriteLine($"  Time      : {swSpan.ElapsedMilliseconds} ms");
//Console.WriteLine($"  Allocated : {FormatBytes(memAfterSpan - memBeforeSpan)} (TRUE ZERO)");

//Console.WriteLine("\n----------------------------------------");
//Console.ForegroundColor = ConsoleColor.Green;
//if(swSpan.ElapsedMilliseconds < swSplit.ElapsedMilliseconds) {
//    double speedup = (double)swSplit.ElapsedMilliseconds / swSpan.ElapsedMilliseconds;
//    Console.WriteLine($"SpanSplitter is {speedup:F2}x FASTER!");
//}
//else {
//    Console.WriteLine("String.Split is still faster in raw time, but look at the memory!");
//}

//double memSaved = (double)(memAfterSplit - memBeforeSplit) / (memAfterSpan - memBeforeSpan + 1);
//Console.WriteLine($"SpanSplitter saved {FormatBytes(memAfterSplit - memBeforeSplit)} of RAM!");
//Console.WriteLine($"SpanSplitter is {memSaved:N0}x more memory efficient!");
//Console.ResetColor();

//Console.WriteLine("\nPress any key to exit.");
//Console.ReadKey();

//static string FormatBytes(long bytes) {
//    string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
//    int i;
//    double dblSByte = bytes;
//    for(i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024) {
//        dblSByte = bytes / 1024.0;
//    }
//    return $"{dblSByte:N2} {Suffix[i]}";
//}


Console.WriteLine("========================================");
Console.WriteLine("    ValueList vs System.Collections.List");
Console.WriteLine("========================================");

const int OuterLoop = 10_000;
const int InnerCount = 100_000;

// --- TEST 2: Wiaoj ValueList<int> ---
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long memBeforeValue = GC.GetTotalAllocatedBytes(true);
Stopwatch swValue = Stopwatch.StartNew();

Span<GameEntity> storage = new GameEntity[16];



for(var i = 0; i < OuterLoop; i++) {
    using var vList = new ValueList<GameEntity>(storage);

    for(var j = 0; j < InnerCount; j++) {
        vList.Add(new GameEntity { Id = j, Health = 100f });
        //vList.AddAlloc() = new GameEntity { Id = j, Health = 100f };
    }
}

swValue.Stop();
long memAfterValue = GC.GetTotalAllocatedBytes(true);

// --- TEST 1: Standard List<int> ---
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long memBeforeList = GC.GetTotalAllocatedBytes(true);
Stopwatch swList = Stopwatch.StartNew();
 
for(int i = 0; i < OuterLoop; i++) {
    // HER SEFERİNDE HEAP ALLOCATION
    List<GameEntity> list = [];
    for(var j = 0; j < InnerCount; j++) 
        list.Add(new GameEntity { Id = j, Health = 100f }); 
}

swList.Stop();
long memAfterList = GC.GetTotalAllocatedBytes(true);



// --- SONUÇLAR ---
Console.WriteLine($"\n[List<int>]");
Console.WriteLine($"  Time      : {swList.ElapsedMilliseconds} ms");
Console.WriteLine($"  Allocated : {FormatBytes(memAfterList - memBeforeList)}");

Console.WriteLine($"\n[ValueList<int>]");
Console.WriteLine($"  Time      : {swValue.ElapsedMilliseconds} ms");
Console.WriteLine($"  Allocated : {FormatBytes(memAfterValue - memBeforeValue)} (TRUE ZERO)");

Console.WriteLine("\n----------------------------------------");
double speedRatio = (double)swList.ElapsedMilliseconds / swValue.ElapsedMilliseconds;
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"ValueList is {speedRatio:F2}x faster!");
Console.WriteLine($"Memory used is {((double)(memAfterList - memBeforeList) / (memAfterValue - memBeforeValue + 1)):N0}x less!");
Console.ResetColor();

Console.ReadKey();

static string FormatBytes(long bytes) => $"{bytes / 1024.0 / 1024.0:N2} MB";
 
public class GameEntity {
    public int Id;
    public float Health;
    public float X, Y, Z;
}