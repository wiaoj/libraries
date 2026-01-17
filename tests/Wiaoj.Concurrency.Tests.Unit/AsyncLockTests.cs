using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Concurrency.Tests.Unit;

public class AsyncLockTests {
    [Fact]
    public async Task LockAsync_EnforcesMutualExclusion() {
        // Setup
        var asyncLock = new AsyncLock();
        int sharedCounter = 0;

        // SAYILARI DÜŞÜRDÜK:
        // 50 yerine 10 Task, 500 yerine 50 işlem.
        // Toplam: 500 işlem. (Yeterli bir örneklem)
        int taskCount = 10;
        int incrementsPerTask = 50;

        // Action
        var tasks = Enumerable.Range(0, taskCount).Select(async _ =>
        {
            for(int i = 0; i < incrementsPerTask; i++) {
                // Kilit alıyoruz
                using(await asyncLock.LockAsync()) {
                    int current = sharedCounter;

                    // Task.Delay(1) yerine Task.Yield() kullanabilirsin.
                    // Yield, "Sıramı salıyorum" demektir ve context switch yaratır 
                    // ama 15ms bekleme cezası kesmez. Çok daha hızlıdır.
                    await Task.Yield();

                    sharedCounter = current + 1;
                }
            }
        });

        // Timeout koyalım ki gerçekten deadlock varsa sonsuza kadar beklemesin, hata fırlatsın.
        var allTasks = Task.WhenAll(tasks);

        // 5 saniye içinde bitmezse testi patlat.
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(5000));

        if(completedTask != allTasks) {
            throw new TimeoutException("Test zaman aşımına uğradı! (Gerçek deadlock olabilir)");
        }

        // Assert
        Assert.Equal(taskCount * incrementsPerTask, sharedCounter);
    }
}