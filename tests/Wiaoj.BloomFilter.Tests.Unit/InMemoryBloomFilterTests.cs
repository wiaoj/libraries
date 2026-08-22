using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Component", "InMemoryBloomFilter")]
public sealed class InMemoryBloomFilterTests {
    private readonly BloomFilterOptions _options = new();

    // ────────────────────────────────────────────────────────────────────────
    // 1. BASIC CONFORMANCE & REGRESSION TESTS
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_Item_Should_Always_Be_Contained() {
        BloomFilterConfiguration config = new("test", 1000, Percentage.FromDouble(0.01));
        InMemoryBloomFilter filter = new(config, null, NullLogger.Instance, this._options, TimeProvider.System);
        byte[] item = Encoding.UTF8.GetBytes("secret-key");

        filter.Add(item);

        Assert.True(filter.Contains(item));
        Assert.True(filter.IsDirty);
    }

    // 🌟 REGRESSION TEST: Verifies that custom/non-zero HashSeed works identically for Add and Contains
    [Theory]
    [InlineData(0x7769616F6A5F6266)] // Default factory seed
    [InlineData(0xDEADBEEFCAFE)]     // Custom large seed
    [InlineData(123456789)]          // Arbitrary positive seed
    [InlineData(-987654321)]         // Negative seed
    public void Add_WithCustomHashSeed_ShouldBeContained_ForBothByteAndCharSpans(long customSeed) {
        // Arrange: Custom non-zero seed ile config oluşturuyoruz
        BloomFilterConfiguration config = new BloomFilterConfiguration("seed-test", 1000, Percentage.FromDouble(0.01))
            .WithHashSeed(customSeed);

        InMemoryBloomFilter filter = new(config, null, NullLogger.Instance, this._options, TimeProvider.System);

        const string testKey = "webhook:order:ORD-9999";
        byte[] testKeyBytes = Encoding.UTF8.GetBytes(testKey);

        // Act 1: Add via byte span -> must be found via byte and char spans
        filter.Add(testKeyBytes);
        Assert.True(filter.Contains(testKeyBytes), $"Contains(byte[]) failed for HashSeed: {customSeed:X}");
        Assert.True(filter.Contains(testKey.AsSpan()), $"Contains(char[]) failed for HashSeed: {customSeed:X}");

        // Act 2: Add via char span -> must be found via byte and char spans
        const string secondKey = "webhook:order:ORD-8888";
        filter.Add(secondKey.AsSpan());
        Assert.True(filter.Contains(secondKey.AsSpan()), $"Contains(char[]) failed after char Add for HashSeed: {customSeed:X}");
        Assert.True(filter.Contains(Encoding.UTF8.GetBytes(secondKey)), $"Contains(byte[]) failed after char Add for HashSeed: {customSeed:X}");
    }

    // 🌟 FACTORY INTEGRATION TEST: ConfigurationFactory'den çıkan üretim config'i ile doğrulama
    [Fact]
    public void Add_And_Contains_UsingConfigurationFactory_ShouldBeConsistent() {
        BloomFilterConfigurationFactory factory = new();
        BloomFilterConfiguration config = factory.Create("factory-test", 10_000, 0.01);

        InMemoryBloomFilter filter = new(config, null, NullLogger.Instance, this._options, TimeProvider.System);

        for(int i = 0; i < 100; i++) {
            string item = $"item-{i}";
            filter.Add(item.AsSpan());
            Assert.True(filter.Contains(item.AsSpan()), $"Item '{item}' was not found with factory seed {config.HashSeed:X}");
        }
    }

    [Fact]
    public void False_Positive_Rate_Should_Be_Within_Reasonable_Bound() {
        // 10.000 öğe kapasiteli filtreye 10.000 öğe ekle
        int capacity = 10000;
        BloomFilterConfiguration config = new("test", capacity, Percentage.FromDouble(0.01));
        InMemoryBloomFilter filter = new(config, null, NullLogger.Instance, this._options, TimeProvider.System);

        for(int i = 0; i < capacity; i++)
            filter.Add(Encoding.UTF8.GetBytes($"item-{i}"));

        // Filtrede olmayan 10.000 öğeyi sor
        int falsePositives = 0;
        for(int i = capacity; i < capacity * 2; i++) {
            if(filter.Contains(Encoding.UTF8.GetBytes($"item-{i}")))
                falsePositives++;
        }

        double actualRate = (double)falsePositives / capacity;
        // %1 hedeflemiştik, %2'den fazla sapma olmamalı (istatistiksel tolerans)
        Assert.True(actualRate < 0.02, $"Actual FP Rate was: {actualRate}");
    }
}