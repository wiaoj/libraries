using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Concurrency.Tests.Unit;
public class AtomicTests {
    [Fact]
    public void Increment_IsThreadSafe_UnderHighContention() {
        // 1. Setup
        int targetValue = 0;
        int unsafeValue = 0;
        int iterationCount = 100_000;

        // 2. Action: Paralel olarak 100 bin kere artırma işlemi
        Parallel.For(0, iterationCount, _ => {
            // Güvenli Yöntem
            Atomic.Increment(ref targetValue);

            // Güvensiz Yöntem (Yarış Durumu Göstergesi)
            unsafeValue++;
        });

        // 3. Assert
        // Atomic sınıfı tam olarak 100.000 olmalı.
        Assert.Equal(iterationCount, targetValue);

        // Standart int artırma işlemi (++) atomik olmadığı için 
        // muhtemelen 100.000'den az çıkacaktır (Race Condition).
        // Bu assert bazen şans eseri tutabilir ama genelde tutmaz, 
        // amacı farkı göstermektir.
        Assert.NotEqual(iterationCount, unsafeValue);
    }

    [Fact]
    public void CompareExchange_WorksCorrectly() {
        int value = 10;

        // Başarılı senaryo: Değer 10 ise 20 yap.
        bool result1 = Atomic.CompareExchange(ref value, 20, 10);
        Assert.True(result1);
        Assert.Equal(20, value);

        // Başarısız senaryo: Değer artık 20, ama biz 10 sanıp 30 yapmaya çalışıyoruz.
        bool result2 = Atomic.CompareExchange(ref value, 30, 10);
        Assert.False(result2);
        Assert.Equal(20, value); // Değişmemeli
    }
}