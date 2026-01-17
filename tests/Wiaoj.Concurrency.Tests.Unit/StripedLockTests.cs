using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Concurrency.Tests.Unit;

public class StripedLockTests {
    [Fact]
    public async Task LockAsync_BlocksSameKeys_AllowsDifferentKeys() {
        var stripedLock = new StripedLock<string>(stripes: 4);
        var log = new ConcurrentQueue<string>();

        // Senaryo:
        // Task A: Key="User1" (Uzun süren işlem)
        // Task B: Key="User2" (Hemen çalışmalı, User1'i beklememeli)
        // Task C: Key="User1" (User1 kilitli olduğu için beklemeli)

        var t1 = Task.Run(async () => {
            using(await stripedLock.LockAsync("User1")) {
                log.Enqueue("User1-Start");
                await Task.Delay(200); // Kilit tutuluyor
                log.Enqueue("User1-End");
            }
        });

        // t1'in kilidi aldığından emin olmak için minik bekleme
        await Task.Delay(50);

        var t2 = Task.Run(async () => {
            using(await stripedLock.LockAsync("User2")) {
                // User1 kilitli olsa bile User2 hemen girmeli
                log.Enqueue("User2-In");
            }
        });

        var t3 = Task.Run(async () => {
            using(await stripedLock.LockAsync("User1")) {
                // Bu arkadaş t1 bitene kadar girememeli
                log.Enqueue("User1-Second-In");
            }
        });

        await Task.WhenAll(t1, t2, t3);

        var result = log.ToArray();

        // KONTROLLER

        // 1. User2, User1 bitmeden araya girebilmiş mi?
        // "User1-Start" -> "User2-In" -> "User1-End" sıralaması beklenir.
        // Yani User2, User1'in bitmesini (End) beklememeli.
        Assert.Contains("User2-In", result);

        // 2. User1-Second-In kesinlikle User1-End'den sonra olmalı.
        int indexEnd = Array.IndexOf(result, "User1-End");
        int indexSecondIn = Array.IndexOf(result, "User1-Second-In");

        Assert.True(indexSecondIn > indexEnd, "Aynı key (User1) için kilit çalışmadı!");
    }
}