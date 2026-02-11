namespace Wiaoj.Primitives.Tests.Unit.Obfuscation;
public sealed class PublicIdConcurrencyTests {
    [Fact]
    public void Configure_Should_Be_ThreadSafe() {
        // Aynı anda birçok thread konfigürasyonu değiştirmeye çalışırsa hata almamalıyız
        Parallel.For(0, 100, i => {
            PublicId.Configure($"Seed-{i}");
        });

        // Sonuçta sistem stabil kalmalı
        PublicId pid = new(12345L);
        Assert.NotEmpty(pid.ToString());
    }
}