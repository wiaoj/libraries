namespace Wiaoj.Concurrency.Tests.Unit;

public class AtomicTests {
    [Fact(Skip = "Non-deterministic by design - demonstrates race condition, not a real test")]
    public void Increment_WithoutAtomic_MayLoseUpdates_DueToRaceCondition() {
        int unsafeValue = 0;
        int iterationCount = 100_000;

        Parallel.For(0, iterationCount, _ => {
            unsafeValue++;
        });
         
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

    [Fact]
    public void Update_CorrectlyAppliesLogic_UnderHighContention() {
        // Setup
        int value = 0;
        int iterations = 100_000;

        // Action: 100.000 kere paralel olarak sayıyı 2 ile çarpıp 1 ekleyelim? 
        // Hayır, basit toplama yapalım ama Update metodunu zorlayalım.
        // Logic: x => x + 1

        Parallel.For(0, iterations, _ => {
            // Atomic.Update, işlem başarısız olursa (başka thread araya girerse)
            // otomatik olarak tekrar dener (retry loop).
            Atomic.Update(ref value, current => current + 1);
        });

        // Assert
        Assert.Equal(iterations, value);
    }
}