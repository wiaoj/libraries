using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Concurrency.Tests.Unit;

public class AsyncBarrierTests {
    [Fact]
    public async Task SignalAndWaitAsync_WaitsForAllParticipants() {
        int participantCount = 4;
        var barrier = new AsyncBarrier(participantCount);
        int finishedCount = 0;

        var tasks = Enumerable.Range(0, participantCount).Select(async id => {
            // Biraz rastgele bekleme ekleyelim ki herkes farklı zamanlarda gelsin
            await Task.Delay(id * 50);

            // Bariyerde bekle
            await barrier.SignalAndWaitAsync();

            // Bariyeri geçtikten sonra sayacı artır
            Interlocked.Increment(ref finishedCount);
        }).ToArray();

        // Tasks listesi henüz tamamlanmadı, ama içindeki mantığı kontrol edelim.
        // Bariyerin çalışma prensibi gereği, son kişi gelene kadar kimse geçemez.
        // Bu yüzden Task.WhenAll ile hepsini bekleyip sonucu kontrol ediyoruz.

        await Task.WhenAll(tasks);

        Assert.Equal(participantCount, finishedCount);
    }

    [Fact]
    public async Task Barrier_IsReusable_MultiplePhases() {
        // Bariyerin tekrar kullanılabilir (cyclic) olduğunu test edelim.
        var barrier = new AsyncBarrier(3);
        int phase1Count = 0;
        int phase2Count = 0;

        var tasks = Enumerable.Range(0, 3).Select(async _ => {
            // FAZ 1
            await barrier.SignalAndWaitAsync();
            Interlocked.Increment(ref phase1Count);

            // FAZ 2
            await barrier.SignalAndWaitAsync();
            Interlocked.Increment(ref phase2Count);
        });

        await Task.WhenAll(tasks);

        Assert.Equal(3, phase1Count);
        Assert.Equal(3, phase2Count);
    }
}