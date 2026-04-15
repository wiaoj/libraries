namespace Wiaoj.Concurrency.Tests.Unit;

public sealed class AsyncLazyTests {

    [Fact(DisplayName = "1. Thundering Herd: 10.000 task aynı anda saldırsa da factory 1 kez çalışır.")]
    public async Task GetValueAsync_ConcurrentAccess_ExecutesFactoryOnlyOnce() {
        int factoryExecutionCount = 0;
        AsyncLazy<string> lazy = new(async (ct) => {
            await Task.Delay(50);
            Interlocked.Increment(ref factoryExecutionCount);
            return "TestValue";
        });

        int numberOfTasks = 10_000;
        List<Task<string>> tasks = [];

        for(int i = 0; i < numberOfTasks; i++) {
            tasks.Add(Task.Run(async () => await lazy.GetValueAsync()));
        }

        string[] results = await Task.WhenAll(tasks);

        Assert.Equal(1, factoryExecutionCount); // Sadece 1 kez çalışmalı
        Assert.True(lazy.IsValueCreated); // İşlem bitince durum True dönmeli
        foreach(string? result in results) {
            Assert.Equal("TestValue", result);
        }
    }

    [Fact(DisplayName = "2. Retry Mantığı: Factory hata verirse durumu sıfırlar ve tekrar denemeye izin verir.")]
    public async Task GetValueAsync_WhenFactoryThrows_ResetsStateAndAllowsRetry() {
        int executionCount = 0;

        AsyncLazy<int> lazy = new(async (ct) => {
            int current = Interlocked.Increment(ref executionCount);
            await Task.Delay(10);

            if(current == 1) {
                throw new InvalidOperationException("İlk deneme ağ hatası!");
            }

            return 42; // İkinci denemede başarılı olacak
        });

        // Birinci Çağrı: Hata fırlatmalı
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await lazy.GetValueAsync());
        Assert.False(lazy.IsValueCreated); // Henüz başarılı değer oluşmadı

        // İkinci Çağrı: Senin mimarine göre sınıf durumu sıfırladığı için bu sefer başarılı olmalı!
        int result = await lazy.GetValueAsync();

        Assert.Equal(42, result);
        Assert.Equal(2, executionCount); // Factory iki kez tetiklenmiş olmalı
        Assert.True(lazy.IsValueCreated);
    }

    [Fact(DisplayName = "3. İptal Durumu: Başlatma işlemi iptal edilirse, Task iptal edilir ve state sıfırlanır.")]
    public async Task GetValueAsync_WhenCanceled_ThrowsAndResetsState() {
        int executionCount = 0;
        AsyncLazy<string> lazy = new(async (ct) => {
            Interlocked.Increment(ref executionCount);
            await Task.Delay(5000, ct); // Uzun bir işlem, iptal edilmeyi bekliyor
            return "Success";
        });

        using var cts = new CancellationTokenSource();

        // Task'i başlat ama beklemeye geçmeden hemen iptal et
        var initTask = lazy.GetValueAsync(cts.Token).AsTask();
        cts.Cancel();

        // 1. İptal exception'ı fırlatmalı
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await initTask);

        // 2. Kendi CancellationToken'ımızı yollamadığımız yeni bir istek atarsak, 
        // sistem iptal edilen state'i sıfırladığı için tekrar çalışmaya başlamalı.
        var retryTask = lazy.GetValueAsync(CancellationToken.None);

        // Testin takılmaması için hızlıca factory'nin 2. kez tetiklendiğini doğrulayalım
        await Task.Delay(50);
        Assert.Equal(2, executionCount);
    }

    [Fact(DisplayName = "4. Pre-Computed: Hazır değer ile başlatıldığında IsValueCreated anında True döner.")]
    public async Task Constructor_WithPrecomputedValue_ReturnsSynchronously() {
        // Hazır bir değer veriyoruz
        AsyncLazy<string> lazy = new("AlreadyLoaded");

        // Anında True olmalı, arka planda hiçbir Task yaratmamalı
        Assert.True(lazy.IsValueCreated);

        // GetValueAsync'i await etmeden Task durumuna bakıyoruz
        var valueTask = lazy.GetValueAsync();
        Assert.True(valueTask.IsCompletedSuccessfully);

        Assert.Equal("AlreadyLoaded", await valueTask);
    }

    [Fact(DisplayName = "5. Activator Constructor: Parametresiz T tipi için default constructor'ı çalıştırır.")]
    public async Task Constructor_Parameterless_UsesActivator() {
        // List<int> public, parametresiz bir constructor'a sahiptir
        AsyncLazy<List<int>> lazy = new();

        var result = await lazy.GetValueAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // Bu test için sahte bir disposable nesne yaratıyoruz
    private class DummyDisposableModel : IAsyncDisposable {
        public bool IsDisposed { get; private set; }
        public ValueTask DisposeAsync() {
            this.IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact(DisplayName = "6. Disposal: T tipi IAsyncDisposable ise, sınıf Dispose edildiğinde içindeki nesne de Dispose edilir.")]
    public async Task DisposeAsync_WhenValueIsDisposable_DisposesValue() {
        var dummy = new DummyDisposableModel();
        AsyncLazy<DummyDisposableModel> lazy = new(dummy);

        // Değerin yaratıldığından emin ol
        await lazy.GetValueAsync();

        // AsyncLazy'yi dispose et
        await lazy.DisposeAsync();

        // İçindeki nesne de dispose edilmiş olmalı!
        Assert.True(dummy.IsDisposed);
    }

    [Fact(DisplayName = "7. Disposal: Değer henüz yaratılmadıysa Dispose patlamaz, sessizce geçer.")]
    public async Task DisposeAsync_WhenValueNotCreated_DoesNotThrow() {
        AsyncLazy<DummyDisposableModel> lazy = new(async (ct) => {
            await Task.Delay(100);
            return new DummyDisposableModel();
        });

        // Factory'i HİÇ TETİKLEMEDEN direkt dispose ediyoruz
        var exception = await Record.ExceptionAsync(async () => await lazy.DisposeAsync());

        // Assert: Sınıf null referans hatası vermemeli.
        Assert.Null(exception);
    }
}