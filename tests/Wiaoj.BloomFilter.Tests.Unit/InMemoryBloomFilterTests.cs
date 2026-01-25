using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit;

public sealed class InMemoryBloomFilterTests {
    private readonly BloomFilterOptions _options = new();

    [Fact]
    public void Add_Item_Should_Always_Be_Contained() {
        BloomFilterConfiguration config = new("test", 1000, Percentage.FromDouble(0.01));
        InMemoryBloomFilter filter = new(config, null, NullLogger.Instance, this._options, TimeProvider.System);
        var item = Encoding.UTF8.GetBytes("secret-key");

        filter.Add(item);

        Assert.True(filter.Contains(item));
        Assert.True(filter.IsDirty);
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