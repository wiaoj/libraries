namespace Wiaoj.Concurrency.Tests.Unit;

public sealed class AsyncLazyDeepEdgeCaseTests {

    [Fact(DisplayName = "Edge Case 1: Factory kendisini çağırırsa sistem kilitlenir (Deadlock).")]
    public async Task GetValueAsync_CircularDependency_CausesDeadlock() {
        AsyncLazy<string>? lazy = null;

        // Kendi kendini çağıran zehirli bir factory
        lazy = new AsyncLazy<string>(async (ct) => {
            await Task.Delay(10);

            // DİKKAT: Factory henüz bitmeden, değerini kendisinden istiyor!
            // Bu bir mimari hatadır ve deadlock ile sonuçlanmalıdır.
            return await lazy!.GetValueAsync();
        });

        // 1 saniye içinde bitmezse timeout atacak
        Task timeoutTask = Task.Delay(1000);
        Task<string> lazyTask = lazy.GetValueAsync().AsTask();

        Task completedTask = await Task.WhenAny(lazyTask, timeoutTask);

        // Assert: İşlem asla bitmemeli, Task.Delay (Timeout) kazanmalı.
        // Senin mimarinde bu durum sonsuz beklemeye (deadlock) yol açar.
        Assert.Equal(timeoutTask, completedTask);
    }

    [Fact(DisplayName = "Edge Case 2: İki thread aynı task'i beklerken biri iptal ederse, ikisi de iptal olmalıdır.")]
    public async Task GetValueAsync_SharedTaskCanceledByOne_ThrowsForBoth() {
        // İptal edilmeyi bekleyen uzun bir işlem
        AsyncLazy<string> lazy = new(async (ct) => {
            await Task.Delay(5000, ct);
            return "Başarılı";
        });

        using CancellationTokenSource cts1 = new();
        // cts2 hiçbir zaman iptal edilmeyecek
        using CancellationTokenSource cts2 = new();

        // Thread 1 (İptal edebilen)
        Task<string> task1 = lazy.GetValueAsync(cts1.Token).AsTask();

        // Thread 2 (İptal etmeyecek olan ama aynı sonuca bağlanan)
        Task<string> task2 = lazy.GetValueAsync(cts2.Token).AsTask();

        // Sadece Thread 1 işlemi iptal ediyor!
        cts1.Cancel();

        // Assert: Thread 1 iptal hatası almalı
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task1);

        // KRİTİK ASSERT: İşlem altyapıda iptal edildiği için, iptal talebinde bulunmayan
        // Thread 2 de beklemede kalmamalı ve iptal hatası (veya factory'den gelen hatayı) almalıdır.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task2);
    }

    // Dispose edildiğinde bilerek patlayan sahte bir sınıf
    private class ExplodingDisposable : IAsyncDisposable {
        public ValueTask DisposeAsync() {
            throw new OutOfMemoryException("Dispose sırasında kritik bellek hatası!");
        }
    }

    [Fact(DisplayName = "Edge Case 3: İçteki nesne Dispose sırasında patlarsa, AsyncLazy bunu yutmalı ve uygulamayı çökertmemelidir.")]
    public async Task DisposeAsync_WhenInnerObjectThrows_SwallowsException() {
        ExplodingDisposable explodingObject = new();
        AsyncLazy<ExplodingDisposable> lazy = new(explodingObject);

        // Nesneyi kullanıyoruz (Initialize)
        await lazy.GetValueAsync();

        // Action: Sistemi kapatıyoruz
        // Eğer sınıfındaki try-catch mekanizması (Debug.WriteLine kısmı) olmasaydı
        // bu satır OutOfMemoryException fırlatıp testi (ve gerçekte uygulamayı) patlatırdı.
        Exception exception = await Record.ExceptionAsync(async () => await lazy.DisposeAsync());

        // Assert: AsyncLazy, hatayı yuttuğu için dışarıya Exception sızmamalıdır (null olmalıdır).
        Assert.Null(exception);
    }

    [Fact(DisplayName = "Edge Case 4: Factory çalıştıktan sonra delegate bellekten temizlenmeli (Garbage Collection).")]
    public async Task ExecuteFactoryAsync_AfterCompletion_ClearsFactoryReference() {
        WeakReference weakRef = null!;
        AsyncLazy<string> lazy = null!;

        // 1. Devasa veriyi ve AsyncLazy'yi yaratma işlemini izole bir yerel metoda alıyoruz.
        // Böylece "hugeData" değişkeni scope bittiğinde otomatik olarak yalıtılacak.
        void SetupData() {
            byte[] hugeData = new byte[10 * 1024 * 1024]; // 10 MB
            weakRef = new WeakReference(hugeData);

            // Factory closure'ı devasa veriyi hapseder
            lazy = new AsyncLazy<string>(async (ct) => {
                await Task.Delay(1, ct); // Asenkron state-machine tetiklemesi
                return $"Veri boyutu: {hugeData.Length}";
            });
        }

        // Setup'ı çalıştır. Bu noktada veriyi tutan TEK şey AsyncLazy içindeki _factory delegesidir.
        SetupData();

        // 2. Factory'i çalıştır. (Artık null değil, dolu veriyi okuyacak)
        await lazy.GetValueAsync();

        // 3. Çöp Toplayıcıyı (Garbage Collector) tam güçle zorla
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect(); // .NET'te asenkron state-machine temizliği için bazen çift collect gerekebilir

        // 4. Assert: Senin kodundaki "Interlocked.Exchange(ref this._factory, null);" 
        // satırı sayesinde factory delegesi silindiği için, devasa veri de GC tarafından yok edilmiş olmalı.
        Assert.False(weakRef.IsAlive, "Factory delegate bellekten temizlenmedi! Memory Leak var.");
    }
}