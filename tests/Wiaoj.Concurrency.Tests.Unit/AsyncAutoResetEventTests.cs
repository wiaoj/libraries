using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Concurrency.Tests.Unit;

public class AsyncAutoResetEventTests {
    [Fact]
    public async Task Set_ReleasesOnlyOneWaiter() {
        var autoResetEvent = new AsyncAutoResetEvent(false);
        int releaseCount = 0;

        // 5 tane görev başlatıyoruz, hepsi sinyal bekliyor.
        var waitingTasks = Enumerable.Range(0, 5).Select(async _ => {
            await autoResetEvent.WaitAsync();
            Interlocked.Increment(ref releaseCount);
        }).ToList();

        // 1. Sinyal
        autoResetEvent.Set();
        await Task.Delay(50); // Context switch için minik bekleme
        Assert.Equal(1, releaseCount); // Sadece 1 kişi uyanmalı

        // 2. Sinyal
        autoResetEvent.Set();
        await Task.Delay(50);
        Assert.Equal(2, releaseCount); // Şimdi 2. kişi uyanmalı

        // Kalanları temizleyelim (test takılmasın)
        autoResetEvent.Set();
        autoResetEvent.Set();
        autoResetEvent.Set();

        await Task.WhenAll(waitingTasks);
        Assert.Equal(5, releaseCount);
    }
}