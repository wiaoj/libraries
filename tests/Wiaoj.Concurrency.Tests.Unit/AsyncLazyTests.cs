namespace Wiaoj.Concurrency.Tests.Unit;
public class AsyncLazyTests {
    [Fact]
    public async Task GetValueAsync_ConcurrentAccess_ExecutesFactoryOnlyOnce() {
        // 1. SETUP: Sayacımızı ve test edilecek nesneyi hazırlıyoruz.
        int factoryExecutionCount = 0;

        // Bu factory çalıştığında sayacı thread-safe şekilde artıracak ve bir değer dönecek.
        AsyncLazy<string> lazy = new(async (ct) => {
            // Simüle edilmiş bir gecikme (gerçek dünya senaryosu)
            await Task.Delay(50);

            // Interlocked, thread-safe artırma işlemidir. 
            // Eğer burası aynı anda 2 kere çalışırsa sayaç 2 olur.
            Interlocked.Increment(ref factoryExecutionCount);

            return "TestValue";
        });

        // 2. ACTION: Aynı anda saldırma aşaması (Concurrency)
        int numberOfTasks = 10_000;
        List<Task<string>> tasks = new();

        // Tüm task'leri hazırlıyoruz ama henüz hepsi aynı anda sonucunu bekleyecek.
        for(int i = 0; i < numberOfTasks; i++) {
            // Her döngüde bir task başlatıp listeye ekliyoruz.
            // Not: Task.Run kullanıyoruz ki thread pool üzerinde gerçekten paralel çalışsınlar.
            tasks.Add(Task.Run(async () => await lazy.GetValueAsync()));
        }

        // Tüm task'lerin bitmesini bekle
        var results = await Task.WhenAll(tasks);

        // 3. ASSERT: Doğrulama aşaması

        // Kural 1: Factory gerçekten sadece 1 kere çalışmış olmalı.
        Assert.Equal(1, factoryExecutionCount);

        // Kural 2: Dönen 100 sonucun hepsi de aynı olmalı ("TestValue").
        foreach(var result in results) {
            Assert.Equal("TestValue", result);
        }
    }

    [Fact]
    public async Task GetValueAsync_HandlesExceptions_AndCachesFailure() {
        // Senaryo: Eğer factory hata fırlatırsa ne oluyor?
        int executionCount = 0;

        AsyncLazy<int> lazy = new(async (ct) => {
            Interlocked.Increment(ref executionCount);
            await Task.Delay(10);
            throw new InvalidOperationException("Boom!");
        });

        // İlk çağrı hata fırlatmalı
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await lazy.GetValueAsync());

        // İkinci çağrı YİNE aynı hatayı fırlatmalı (Hata cache'lenmeli mi? Senin koduna göre evet)
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await lazy.GetValueAsync());

        // Factory yine de sadece 1 kere denenmiş olmalı.
        // (Not: Bazı Lazy implementasyonları hata durumunda tekrar denemeye izin verir, 
        // senin kodun task'i cache'lediği için hatayı da cache'liyor. Bu beklenen davranış mı kontrol et.)
        Assert.Equal(1, executionCount);
    }
}