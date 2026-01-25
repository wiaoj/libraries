using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit;
public sealed class ConcurrencyTests {
    [Fact]
    public async Task Multiple_Threads_Adding_Same_Items_Should_Be_Consistent() {
        // Arrange
        var config = new BloomFilterConfiguration("thread-test", 10000, Percentage.FromDouble(0.01));
        var filter = new InMemoryBloomFilter(config, null, NullLogger.Instance, new BloomFilterOptions(), TimeProvider.System);

        int threadCount = 10;
        int itemsPerThread = 1000;
        var tasks = new List<Task>();

        // Act: 10 farklı koldan aynı anda veri ekle
        for(int i = 0; i < threadCount; i++) {
            int threadId = i;
            tasks.Add(Task.Run(() => {
                for(int j = 0; j < itemsPerThread; j++) {
                    // Bazı thread'ler aynı veriyi eklesin (çakışma testi)
                    filter.Add(Encoding.UTF8.GetBytes($"item-{j}"));
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        // Toplamda sadece 1000 benzersiz item eklendiği için PopCount makul bir seviyede olmalı
        // ve Contains her birini bulabilmeli.
        for(int j = 0; j < itemsPerThread; j++) {
            Assert.True(filter.Contains(Encoding.UTF8.GetBytes($"item-{j}")));
        }

        // IsDirty true olmalı
        Assert.True(filter.IsDirty);
    }

    [Fact]
    public void Parallel_Add_And_Contains_Should_Not_Throw() {
        // Arrange
        var config = new BloomFilterConfiguration("race-test", 5000, Percentage.FromDouble(0.01));
        var filter = new InMemoryBloomFilter(config, null, NullLogger.Instance, new BloomFilterOptions(), TimeProvider.System);

        bool hasError = false;

        // Act: Bir yandan yazarken bir yandan oku (ReaderWriterLockSlim testi)
        Parallel.Invoke(
            () => {
                for(int i = 0; i < 2000; i++)
                    filter.Add(Encoding.UTF8.GetBytes($"write-{i}"));
            },
            () => {
                for(int i = 0; i < 2000; i++) {
                    try { filter.Contains(Encoding.UTF8.GetBytes($"write-{i}")); }
                    catch { hasError = true; }
                }
            }
        );

        // Assert
        Assert.False(hasError, "Concurrent Read/Write caused an exception!");
    }
}