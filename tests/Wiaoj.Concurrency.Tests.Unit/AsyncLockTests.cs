namespace Wiaoj.Concurrency.Tests.Unit;

public sealed class AsyncLockTests {

    [Fact(DisplayName = "1. Kilit, race condition oluşmasını engellemelidir.")]
    public async Task LockAsync_EnforcesMutualExclusion() {
        AsyncLock asyncLock = new();
        int sharedCounter = 0;

        int taskCount = 10;
        int incrementsPerTask = 50;

        IEnumerable<Task> tasks = Enumerable.Range(0, taskCount).Select(async _ => {
            for(int i = 0; i < incrementsPerTask; i++) {
                using(await asyncLock.LockAsync()) {
                    int current = sharedCounter;

                    // İşletim sistemini context switch yapmaya zorluyoruz
                    await Task.Yield();

                    sharedCounter = current + 1;
                }
            }
        });

        Task allTasks = Task.WhenAll(tasks);
        Task completedTask = await Task.WhenAny(allTasks, Task.Delay(5000));

        if(completedTask != allTasks) {
            throw new TimeoutException("Test zaman aşımına uğradı! (Gerçek deadlock olabilir)");
        }

        Assert.Equal(taskCount * incrementsPerTask, sharedCounter);
    }

    [Fact(DisplayName = "2. Bekleme sırasında iptal edilirse Task iptal edilmelidir.")]
    public async Task LockAsync_ThrowOperationCanceledException_WhenCanceled() {
        AsyncLock asyncLock = new();
        using CancellationTokenSource cts = new();

        Task lockerTask = Task.Run(async () => {
            // Bilerek scope'u dispose etmiyoruz (Using kullanmadık)
            AsyncLock.Scope scope = await asyncLock.LockAsync();
        });

        await Task.Delay(50); // Locker'ın kilidi aldığından emin olalım

        // İkinci task kilidi almaya çalışıyor
        Task<AsyncLock.Scope> waitingTask = asyncLock.LockAsync(cts.Token).AsTask();

        cts.Cancel(); // Bekleyeni vur!

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await waitingTask);
    }

    [Fact(DisplayName = "3. Kilit boştayken beklemeden ve memory allocation yapmadan alınmalıdır.")]
    public async Task LockAsync_Uncontended_IsSynchronous_And_AllocationFree() {
        AsyncLock asyncLock = new();

        // Action
        ValueTask<AsyncLock.Scope> valueTask = asyncLock.LockAsync();

        // Assert: Kilit boştayken arka planda asenkron state-machine (Task) 
        // yaratılmazsa IsCompleted TRUE döner. Bu, uygulamanın RAM'ini korur.
        Assert.True(valueTask.IsCompleted);

        // Kilit alındı mı kontrol et
        Assert.True(asyncLock.IsLocked);

        // Temizle
        AsyncLock.Scope scope = await valueTask;
        scope.Dispose();
    }

    [Fact(DisplayName = "4. Using bloğu içinde exception fırlatılsa bile kilit serbest bırakılmalıdır.")]
    public async Task LockAsync_ExceptionInsideUsingBlock_ReleasesLock() {
        AsyncLock asyncLock = new();

        // Action
        try {
            using(await asyncLock.LockAsync()) {
                Assert.True(asyncLock.IsLocked);

                // Sistem çöktü!
                throw new InvalidOperationException("Kritik veritabanı hatası!");
            }
        }
        catch(InvalidOperationException) {
            // Hatayı yakaladık, kilit ne durumda?
        }

        // Assert: 'using' bloğu, exception fırlatılsa bile arka planda finally gibi çalışıp Dispose çağırmalı.
        Assert.False(asyncLock.IsLocked, "Kilit hala tutuluyor! Exception durumunda deadlock riski var.");
    }

    [Fact(DisplayName = "5. Kilit bırakıldığında sıradaki bekleyen task çalışmaya başlamalıdır.")]
    public async Task LockAsync_ReleasingLock_AllowsNextWaiter() {
        AsyncLock asyncLock = new();
        bool secondTaskEntered = false;

        AsyncLock.Scope scope1 = await asyncLock.LockAsync();

        // İkinci task kilidi beklemeye başlar
        Task task2 = Task.Run(async () => {
            using(await asyncLock.LockAsync()) {
                secondTaskEntered = true;
            }
        });

        // Küçük bir gecikme verip task2'nin gerçekten beklediğini doğrulayalım
        await Task.Delay(50);
        Assert.False(secondTaskEntered, "İkinci task, kilit bırakılmadan içeri girdi!");

        // İlk kilidi bırak
        scope1.Dispose();

        // İkinci task'in bitmesini bekle
        await task2;

        Assert.True(secondTaskEntered, "İlk kilit bırakıldıktan sonra ikinci task içeri giremedi.");
        Assert.False(asyncLock.IsLocked, "Tüm işlemler bittikten sonra kilit açık kalmalı.");
    }

    [Fact(DisplayName = "6. Kilit reentrant DEĞİLDİR. Aynı task içinde ikinci kez alınmaya çalışılırsa deadlock olur.")]
    public async Task LockAsync_IsNotReentrant_WillTimeoutIfCalledTwice() {
        // Bu sınıf bir SemaphoreSlim sarmalayıcısıdır. Thread-affinity (hangi thread'in çağırdığı) kavramı yoktur.
        // Bu yüzden kilidi alan kişi, bırakmadan tekrar almaya çalışırsa KENDİNİ kitler.
        // Bu test, sistemin yanlışlıkla (magic) reentrant davranmadığını kanıtlar.

        AsyncLock asyncLock = new();
        AsyncLock.Scope scope = await asyncLock.LockAsync();

        // Aynı async flow (task) içinde kilidi tekrar almaya çalışıyoruz. 
        // 100ms içinde alamazsa timeout dönecek.
        Task<AsyncLock.Scope> secondLockTask = asyncLock.LockAsync().AsTask();
        Task timeoutTask = Task.Delay(100);

        Task finishedTask = await Task.WhenAny(secondLockTask, timeoutTask);

        // Assert: İkinci kilit alma işlemi bitmemiş olmalı (timeout çalışmalı).
        Assert.Equal(timeoutTask, finishedTask);

        scope.Dispose(); // Kilidi temizle
    }
}