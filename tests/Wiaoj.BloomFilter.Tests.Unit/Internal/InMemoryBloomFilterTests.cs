using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

public class InMemoryBloomFilterTests {
    private readonly BloomFilterContext _context;
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public InMemoryBloomFilterTests() {
        BloomFilterOptions options = new();
        this._context = new BloomFilterContext(
            Storage: new InMemoryBloomFilterStorage(),
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    private InMemoryBloomFilter CreateFilter(long expectedItems = 10_000, double errorRate = 0.01) {
        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("unit-test-filter"), expectedItems, errorRate);
        return new InMemoryBloomFilter(config, this._context);
    }

    public sealed class AddAndContainsMethods : InMemoryBloomFilterTests {
        [Fact]
        public void Should_ReturnTrueOnContains_When_ItemWasAdded() {
            // Arrange
            using InMemoryBloomFilter filter = CreateFilter();
            byte[] item = Encoding.UTF8.GetBytes("test-item-key");

            // Act
            bool added = filter.Add(item);
            bool contains = filter.Contains(item);

            // Assert
            Assert.True(added);
            Assert.True(contains);
            Assert.True(filter.IsDirty);
        }

        [Fact]
        public void Should_ReturnFalseOnContains_When_ItemWasNotAdded() {
            // Arrange
            using InMemoryBloomFilter filter = CreateFilter();
            byte[] existingItem = Encoding.UTF8.GetBytes("existing-item");
            byte[] missingItem = Encoding.UTF8.GetBytes("missing-item");

            // Act
            filter.Add(existingItem);
            bool contains = filter.Contains(missingItem);

            // Assert
            Assert.False(contains);
        }

        [Fact]
        public void Should_SupportStringOverloads_EquivalentlyToByteSpan() {
            // Arrange
            using InMemoryBloomFilter filter = CreateFilter();
            string key = "account:uuid:48392-48291";

            // Act
            bool added = filter.Add(key.AsSpan());
            bool contains = filter.Contains(key.AsSpan());
            bool containsRawBytes = filter.Contains(Encoding.UTF8.GetBytes(key));

            // Assert
            Assert.True(added);
            Assert.True(contains);
            Assert.True(containsRawBytes);
        }
    }

    public sealed class FalsePositiveRateValidation : InMemoryBloomFilterTests {
        [Fact]
        public void Should_AdhereToConfiguredFalsePositiveRate() {
            // Arrange
            const int insertedCount = 10_000;
            const int testCount = 50_000;
            const double targetErrorRate = 0.02; // 2%

            using InMemoryBloomFilter filter = CreateFilter(expectedItems: insertedCount, errorRate: targetErrorRate);

            for(int i = 0; i < insertedCount; i++) {
                filter.Add($"element-id-{i}");
            }

            // Act: Test elements that were NEVER added
            int falsePositives = 0;
            for(int i = insertedCount; i < insertedCount + testCount; i++) {
                if(filter.Contains($"element-id-{i}")) {
                    falsePositives++;
                }
            }

            double actualFpRate = (double)falsePositives / testCount;

            // Assert: Allow statistical margin (target + 0.01)
            Assert.True(actualFpRate <= targetErrorRate + 0.01,
                $"Observed FP rate ({actualFpRate:P2}) exceeded target threshold ({targetErrorRate:P2})");
        }
    }

    public sealed class PersistenceMethods : InMemoryBloomFilterTests {
        [Fact]
        public async Task Should_PersistAndReloadStateCorrectly() {
            // Arrange
            InMemoryBloomFilterStorage storage = new();
            BloomFilterContext contextWithStorage = this._context with { Storage = storage };
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("persist-filter"), 5_000, 0.01);

            using InMemoryBloomFilter originalFilter = new(config, contextWithStorage);
            originalFilter.Add("saved-item-1");
            originalFilter.Add("saved-item-2");

            // Act: Save to storage
            await originalFilter.SaveAsync();
            Assert.False(originalFilter.IsDirty);

            // Create a second filter instance and reload
            using InMemoryBloomFilter reloadedFilter = new(config, contextWithStorage);
            await reloadedFilter.ReloadAsync();

            // Assert
            Assert.True(reloadedFilter.Contains("saved-item-1"));
            Assert.True(reloadedFilter.Contains("saved-item-2"));
            Assert.False(reloadedFilter.Contains("non-saved-item"));
            Assert.Equal(originalFilter.GetPopCount(), reloadedFilter.GetPopCount());
        }
    }

    public sealed class ConcurrencySafety : InMemoryBloomFilterTests {
        [Fact]
        public void Should_HandleConcurrentAdditions_WithoutDataRaces() {
            // Arrange
            using InMemoryBloomFilter filter = CreateFilter(expectedItems: 20_000);
            const int totalItems = 10_000;

            // Act
            Parallel.For(0, totalItems, i => {
                filter.Add($"concurrent-item-{i}");
            });

            // Assert
            for(int i = 0; i < totalItems; i++) {
                Assert.True(filter.Contains($"concurrent-item-{i}"));
            }
        }
    }

    public sealed class HashSeedVariations : InMemoryBloomFilterTests {
        [Theory]
        [InlineData(0x7769616F6A5F6266)] // Default factory seed
        [InlineData(0xDEADBEEFCAFE)]     // Custom large positive seed
        [InlineData(123456789)]          // Arbitrary positive seed
        [InlineData(-987654321)]         // Negative seed
        public void Should_WorkConsistently_AcrossByteAndCharSpans_WithVariousSeeds(long customSeed) {
            // Arrange
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("seed-test"), 1_000, 0.01, customSeed);
            using InMemoryBloomFilter filter = new(config, this._context);

            const string testKey = "webhook:order:ORD-9999";
            byte[] testKeyBytes = Encoding.UTF8.GetBytes(testKey);

            // Act 1: Add via byte span
            filter.Add(testKeyBytes);
            Assert.True(filter.Contains(testKeyBytes));
            Assert.True(filter.Contains(testKey.AsSpan()));

            // Act 2: Add via char span
            const string secondKey = "webhook:order:ORD-8888";
            filter.Add(secondKey.AsSpan());
            Assert.True(filter.Contains(secondKey.AsSpan()));
            Assert.True(filter.Contains(Encoding.UTF8.GetBytes(secondKey)));
        }
    }

    public sealed class ConcurrentReadWriteSafety : InMemoryBloomFilterTests {
        [Fact]
        public void Should_NotThrowExceptions_DuringSimultaneousReadsAndWrites() {
            // Arrange
            using InMemoryBloomFilter filter = CreateFilter(expectedItems: 10_000);
            bool hasError = false;

            // Act: Concurrently write and read to stress ReaderWriterLockSlim
            Parallel.Invoke(
                () => {
                    for(int i = 0; i < 2_000; i++) {
                        filter.Add(Encoding.UTF8.GetBytes($"write-key-{i}"));
                    }
                },
                () => {
                    for(int i = 0; i < 2_000; i++) {
                        try {
                            filter.Contains(Encoding.UTF8.GetBytes($"write-key-{i}"));
                        }
                        catch {
                            hasError = true;
                        }
                    }
                }
            );

            // Assert
            Assert.False(hasError, "Concurrent Read/Write caused an unexpected exception.");
        }
    }
}