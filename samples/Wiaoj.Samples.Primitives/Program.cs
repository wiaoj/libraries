//using System.Buffers;
//using System.Diagnostics;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using Wiaoj.Primitives.Buffers;

////Console.WriteLine("====================================================");
////Console.WriteLine("    HUGE DATA TEST: SpanSplitter vs String.Split    ");
////Console.WriteLine("    (50,000 Segments / 1MB+ String)                 ");
////Console.WriteLine("====================================================");

////// 1. Devasa veriyi hazırla (Yaklaşık 1.2 MB string)
////Console.WriteLine("Preparing huge data...");
////StringBuilder sb = new();
////for(int i = 0; i < 50_000; i++) {
////    sb.Append(i.ToString("D6")); // "000001", "000002" ...
////    if(i < 49_999) sb.Append(',');
////}
////string hugeData = sb.ToString();
////const int Iterations = 500; // Toplamda 25 Milyon segment işlenecek

////Console.WriteLine($"Huge Data Length: {hugeData.Length:N0} chars");
////Console.WriteLine($"Total Segments to process: {(Iterations * 50_000):N0}\n");

////// --- TEST 1: Standard String.Split ---
////GC.Collect();
////GC.WaitForPendingFinalizers();
////GC.Collect();
////long memBeforeSplit = GC.GetTotalAllocatedBytes(true);
////Stopwatch swSplit = Stopwatch.StartNew();

////long totalLength1 = 0;
////for(int i = 0; i < Iterations; i++) {
////    // HER DÖNGÜDE: 1 Dizi + 50.000 yeni string nesnesi! (GC FELAKETİ)
////    string[] parts = hugeData.Split(',');
////    foreach(var p in parts) {
////        totalLength1 += p.Length;
////    }
////}

////swSplit.Stop();
////long memAfterSplit = GC.GetTotalAllocatedBytes(true);

////// --- TEST 2: Wiaoj SpanSplitter ---
////GC.Collect();
////GC.WaitForPendingFinalizers();
////GC.Collect();
////long memBeforeSpan = GC.GetTotalAllocatedBytes(true);
////Stopwatch swSpan = Stopwatch.StartNew();

////long totalLength2 = 0;
////// SearchValues'ı bir kere oluştur (Best Practice)
////SearchValues<char> sv = SearchValues.Create([',']);

////for(int i = 0; i < Iterations; i++) {
////    // 0 ALLOCATION: Sadece pointer kaydırarak ilerler
////    foreach(var part in hugeData.AsSpan().SplitValue(sv)) {
////        totalLength2 += part.Length;
////    }
////}

////swSpan.Stop();
////long memAfterSpan = GC.GetTotalAllocatedBytes(true);

////// --- RAPOR ---
////Console.WriteLine($"\n[Standard String.Split]");
////Console.WriteLine($"  Time      : {swSplit.ElapsedMilliseconds} ms");
////Console.WriteLine($"  Allocated : {FormatBytes(memAfterSplit - memBeforeSplit)}");

////Console.WriteLine($"\n[Wiaoj SpanSplitter]");
////Console.WriteLine($"  Time      : {swSpan.ElapsedMilliseconds} ms");
////Console.WriteLine($"  Allocated : {FormatBytes(memAfterSpan - memBeforeSpan)} (TRUE ZERO)");

////Console.WriteLine("\n----------------------------------------");
////Console.ForegroundColor = ConsoleColor.Green;
////if(swSpan.ElapsedMilliseconds < swSplit.ElapsedMilliseconds) {
////    double speedup = (double)swSplit.ElapsedMilliseconds / swSpan.ElapsedMilliseconds;
////    Console.WriteLine($"SpanSplitter is {speedup:F2}x FASTER!");
////}
////else {
////    Console.WriteLine("String.Split is still faster in raw time, but look at the memory!");
////}

////double memSaved = (double)(memAfterSplit - memBeforeSplit) / (memAfterSpan - memBeforeSpan + 1);
////Console.WriteLine($"SpanSplitter saved {FormatBytes(memAfterSplit - memBeforeSplit)} of RAM!");
////Console.WriteLine($"SpanSplitter is {memSaved:N0}x more memory efficient!");
////Console.ResetColor();

////Console.WriteLine("\nPress any key to exit.");
////Console.ReadKey();

////static string FormatBytes(long bytes) {
////    string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
////    int i;
////    double dblSByte = bytes;
////    for(i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024) {
////        dblSByte = bytes / 1024.0;
////    }
////    return $"{dblSByte:N2} {Suffix[i]}";
////}


//Console.WriteLine("========================================");
//Console.WriteLine("    ValueList vs System.Collections.List");
//Console.WriteLine("========================================");

//const int OuterLoop = 10_000;
//const int InnerCount = 100_000;

//// --- TEST 2: Wiaoj ValueList<int> ---
//GC.Collect();
//GC.WaitForPendingFinalizers();
//GC.Collect();
//long memBeforeValue = GC.GetTotalAllocatedBytes(true);
//Stopwatch swValue = Stopwatch.StartNew();

//Span<int> storage = new int[16];



//for(var i = 0; i < OuterLoop; i++) {
//    using var vList = new ValueList<int>(storage);

//    for(var j = 0; j < InnerCount; j++) {
//        //vList.Add(new GameEntity { Id = j, Health = 100f });
//        vList.Add(j);
//        //vList.AddAlloc() = new GameEntity { Id = j, Health = 100f };
//    }
//}

//swValue.Stop();
//long memAfterValue = GC.GetTotalAllocatedBytes(true);

//// --- TEST 1: Standard List<int> ---
//GC.Collect();
//GC.WaitForPendingFinalizers();
//GC.Collect();
//long memBeforeList = GC.GetTotalAllocatedBytes(true);
//Stopwatch swList = Stopwatch.StartNew();

//for(int i = 0; i < OuterLoop; i++) {
//    // HER SEFERİNDE HEAP ALLOCATION
//    List<int> list = [];
//    for(var j = 0; j < InnerCount; j++) 
//        list.Add(j); 
//        //list.Add(new GameEntity { Id = j, Health = 100f }); 
//}

//swList.Stop();
//long memAfterList = GC.GetTotalAllocatedBytes(true);



//// --- SONUÇLAR ---
//Console.WriteLine($"\n[List<int>]");
//Console.WriteLine($"  Time      : {swList.ElapsedMilliseconds} ms");
//Console.WriteLine($"  Allocated : {FormatBytes(memAfterList - memBeforeList)}");

//Console.WriteLine($"\n[ValueList<int>]");
//Console.WriteLine($"  Time      : {swValue.ElapsedMilliseconds} ms");
//Console.WriteLine($"  Allocated : {FormatBytes(memAfterValue - memBeforeValue)} (TRUE ZERO)");

//Console.WriteLine("\n----------------------------------------");
//double speedRatio = (double)swList.ElapsedMilliseconds / swValue.ElapsedMilliseconds;
//Console.ForegroundColor = ConsoleColor.Green;
//Console.WriteLine($"ValueList is {speedRatio:F2}x faster!");
//Console.WriteLine($"Memory used is {((double)(memAfterList - memBeforeList) / (memAfterValue - memBeforeValue + 1)):N0}x less!");
//Console.ResetColor();

//Console.ReadKey();

//static string FormatBytes(long bytes) => $"{bytes / 1024.0 / 1024.0:N2} MB";

//public struct GameEntity {
//    public int Id;
//    public float Health;
//    public float X, Y, Z;
//}

using System.Diagnostics;
using Wiaoj.Primitives.Snowflake;



//ForceAllStripesTest();
Console.WriteLine("=== SNOWFLAKE VS STRIPED SHOWDOWN ===");

SnowflakeOptions options = new() { NodeId = 1, SequenceBits = 12 };
int totalIds = 500_000_000;
int threadCount = Environment.ProcessorCount; // İşlemci çekirdek sayısı kadar thread

// --- TEST 1: STANDART GENERATOR (Tek Kasiyer) ---
SnowflakeGenerator stdGen = new(options);
Console.WriteLine($"\n1. Senaryo: Standart Generator ({threadCount} Thread Çatışıyor...)");
(long stdTime, long[]? stdIds) = RunTest(stdGen.NextId, totalIds, threadCount);
PrintResult("Standart", stdTime, totalIds, stdIds);

// --- TEST 2: STRIPED GENERATOR (16 Kasiyer/Stripe) ---
StripedSnowflakeGenerator strpGen = new(options, stripeCount: 2);
Console.WriteLine($"\n2. Senaryo: Striped Generator (16 Şerit - Çatışma Azaltıldı...)");
(long strpTime, long[]? strpIds) = RunTest(strpGen.NextId, totalIds, threadCount);
PrintResult("Striped", strpTime, totalIds, strpIds);

// --- ANALİZ ---
double improvement = (double)(stdTime - strpTime) / stdTime * 100;
Console.WriteLine($"\n[ANALİZ]: Striped yapı, Standart yapıya göre %{improvement:F2} daha hızlı.");

Console.WriteLine("\nÇakışma Kontrolü Yapılıyor...");
bool stdUnique = stdIds.Distinct().Count() == totalIds;
bool strpUnique = strpIds.Distinct().Count() == totalIds;

Console.WriteLine($"Standart Benzersiz mi? : {stdUnique}");
Console.WriteLine($"Striped Benzersiz mi?  : {strpUnique}");

Console.WriteLine("\nÖrnek Striped ID Parçalaması (Farklı Node'ları Gör):");


Console.WriteLine("\n--- Şerit Dağılım Analizi ---");
var distribution = strpIds
    .Select(val => (val >> 12) & 1023)
    .GroupBy(node => node)
    .Select(g => new { NodeId = g.Key, Count = g.Count() })
    .OrderBy(x => x.NodeId);

foreach(var stat in distribution) {
    Console.WriteLine($"NodeId: {stat.NodeId} | Üretilen ID Sayısı: {stat.Count:N0}");
}


static (long elapsedMs, long[] results) RunTest(Func<SnowflakeId> nextIdFunc, int total, int threads) {
    long[] results = new long[total];
    Stopwatch sw = Stopwatch.StartNew();

    Parallel.For(0, total, new ParallelOptions { MaxDegreeOfParallelism = threads }, i => {
        results[i] = (long)nextIdFunc();
    });

    sw.Stop();
    return (sw.ElapsedMilliseconds, results);
}

static void PrintResult(string name, long ms, int total, long[] results) {
    double sec = ms / 1000.0;
    double throughput = total / sec;
    Console.WriteLine($"{name} Tamamlandı: {ms} ms");
    Console.WriteLine($"{name} Hız: {throughput:N0} ID/saniye");
}
 
static void ForceAllStripesTest() {
    Console.WriteLine("\n=== 16 ŞERİDİ ZORLAMA TESTİ (Az ID ile) ===");

    var options = new SnowflakeOptions { NodeId = 1 };
    var stripedGen = new StripedSnowflakeGenerator(options, stripeCount: 16);

    // Sadece 160.000 ID (RAM'i yormaz)
    int totalIds = 5_000_000;
    long[] results = new long[totalIds];

    // --- HİLE 1: ThreadPool'a "en az 16 işçi hazırla" diyoruz ---
    ThreadPool.SetMinThreads(16, 16);

    Stopwatch sw = Stopwatch.StartNew();

    // --- HİLE 2: Thread'leri meşgul ediyoruz ---
    Parallel.For(0, totalIds, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => {
        results[i] = (long)stripedGen.NextId(); 
    });

    sw.Stop();

    // Analiz
    var stats = results
        .Select(val => (val >> 12) & 1023)
        .GroupBy(node => node)
        .OrderBy(g => g.Key);

    Console.WriteLine($"\nToplam Çalışan Şerit Sayısı: {stats.Count()}");
    Console.WriteLine("------------------------------------------");
    foreach(var stat in stats) {
        Console.WriteLine($"NodeId: {stat.Key,-3} | Üretilen ID: {stat.Count():N0}");
    }

    Console.WriteLine("Toplam: " + results.Count().ToString("N0"));
}