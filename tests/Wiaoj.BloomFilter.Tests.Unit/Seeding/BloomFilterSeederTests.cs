using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit.Seeding;

public class BloomFilterSeederTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    private (BloomFilterSeeder Seeder, InMemoryBloomFilter Filter) CreateSut(string filterName, long capacity = 10_000) {
        FakeBloomFilterStorage storage = new();
        BloomFilterOptions options = new();

        BloomFilterContext context = new(
            storage,
            new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            NullLogger.Instance,
            options,
            TimeProvider.System,
            this._configFactory
        );

        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse(filterName), capacity, 0.01);
        InMemoryBloomFilter filter = new(config, context);

        ServiceCollection services = new();
        services.AddKeyedSingleton<IPersistentBloomFilter>(filterName, filter);
        IServiceProvider sp = services.BuildServiceProvider();

        BloomFilterSeeder seeder = new(sp, NullLogger<BloomFilterSeeder>.Instance);
        return (seeder, filter);
    }

    public sealed class StringStreamingSeeding : BloomFilterSeederTests {
        [Fact]
        public async Task Should_StreamLargeDataSet_AndPersistToFilterSuccessfully() {
            // Arrange
            const string filterName = "seeder-stream-test";
            const int itemCount = 10_000;
            (BloomFilterSeeder? seeder, InMemoryBloomFilter? filter) = CreateSut(filterName, itemCount);

            async IAsyncEnumerable<string> GenerateStream() {
                for(int i = 0; i < itemCount; i++) {
                    yield return $"stream-item-{i}";
                }
                await Task.CompletedTask;
            }

            // Act
            await seeder.SeedAsync(FilterName.Parse(filterName), GenerateStream(), TestContext.Current.CancellationToken);

            // Assert: All streamed items must exist in the filter
            for(int i = 0; i < itemCount; i++) {
                Assert.True(filter.Contains($"stream-item-{i}"), $"Streamed item {i} was missing.");
            }

            Assert.False(filter.IsDirty); // Verified that SaveAsync was executed at the end
            Assert.True(filter.GetPopCount() > 0);
        }
    }

    public sealed class GenericStreamingSeeding : BloomFilterSeederTests {
        [Fact]
        public async Task Should_StreamGenericObjects_UsingCustomSerializer() {
            // Arrange
            const string filterName = "generic-seeder-test";
            (BloomFilterSeeder? seeder, InMemoryBloomFilter? filter) = CreateSut(filterName, 1_000);

            async IAsyncEnumerable<int> GenerateNumbers() {
                for(int i = 0; i < 500; i++) {
                    yield return i;
                }
                await Task.CompletedTask;
            }

            // Act
            await seeder.SeedAsync(
                FilterName.Parse(filterName),
                GenerateNumbers(),
                num => BitConverter.GetBytes(num),
                TestContext.Current.CancellationToken);

            // Assert
            for(int i = 0; i < 500; i++) {
                Assert.True(filter.Contains(BitConverter.GetBytes(i)));
            }
        }
    }

    public sealed class SeedingEdgeCases : BloomFilterSeederTests {
        [Fact]
        public async Task Should_AbortAndThrowOperationCanceledException_When_CancellationRequestedDuringSeeding() {
            // Arrange
            const string filterName = "seeder-cancel-test";
            (BloomFilterSeeder seeder, InMemoryBloomFilter filter) = CreateSut(filterName, 10_000);
            using CancellationTokenSource cts = new();

            async IAsyncEnumerable<string> CancellableStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) {
                for(int i = 0; i < 1_000; i++) {
                    if(i == 50) cts.Cancel();
                    yield return $"item-{i}";
                }
                await Task.CompletedTask;
            }

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                seeder.SeedAsync(FilterName.Parse(filterName), CancellableStream(cts.Token), cts.Token));
        }

        [Fact]
        public async Task Should_SkipNullItemsGracefully_When_StreamYieldsNull() {
            // Arrange
            const string filterName = "seeder-null-test";
            (BloomFilterSeeder seeder, InMemoryBloomFilter filter) = CreateSut(filterName, 1_000);

            async IAsyncEnumerable<string?> NullableStream() {
                yield return "valid-before";
                yield return null;
                yield return "valid-middle";
                yield return null;
                yield return "valid-after";
                await Task.CompletedTask;
            }

            // Act: SeedAsync should filter out null elements without throwing NullReferenceException
            await seeder.SeedAsync(FilterName.Parse(filterName), NullableStream()!, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(filter.Contains("valid-before"));
            Assert.True(filter.Contains("valid-middle"));
            Assert.True(filter.Contains("valid-after"));
        }

        [Fact]
        public async Task Should_HandleEmptyStream_WithoutErrors() {
            // Arrange
            const string filterName = "seeder-empty-test";
            (BloomFilterSeeder seeder, InMemoryBloomFilter filter) = CreateSut(filterName, 1_000);

            async IAsyncEnumerable<string> EmptyStream() {
                await Task.CompletedTask;
                yield break;
            }

            // Act
            await seeder.SeedAsync(FilterName.Parse(filterName), EmptyStream(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, filter.GetPopCount());
            Assert.False(filter.IsDirty);
        }
    }

    public sealed class ExtremeScaleCalculations {
        [Fact]
        public void Should_CalculateAccurately_ForExtremeScaleCapacities() {
            // Arrange: 1 Billion items with 0.00001 (0.001%) error rate
            const long oneBillionItems = 1_000_000_000L;
            Percentage tinyErrorRate = Percentage.FromDouble(0.00001);

            // Act
            long bits = BloomMath.CalculateOptimalBits(oneBillionItems, tinyErrorRate);
            int hashCount = BloomMath.CalculateOptimalHashCount(bits, oneBillionItems);

            // Assert: Must not overflow to negative and provide mathematically sound dimensions
            Assert.True(bits > 0);
            Assert.True(bits > 20_000_000_000L); // ~23.9 Billion bits (~2.98 GB)
            Assert.InRange(hashCount, 15, 20);   // ~17 hash functions
        }
    }

    public sealed class FastModuloBoundaryTests {
        [Fact]
        public void Should_NotOverflowOrExceedBounds_When_HashesAreAtMaxUInt64() {
            // Arrange
            const ulong maxH1 = ulong.MaxValue;
            const ulong maxH2 = ulong.MaxValue;
            const long hugeSize = 10_000_000_000L; // 10 Billion bits

            // Act & Assert: Test boundary values across multiple hash iterations
            for(int i = 0; i < 20; i++) {
                long pos = BloomHasher.GetBitPosition(maxH1, maxH2, i, hugeSize);
                Assert.InRange(pos, 0, hugeSize - 1);
            }
        }
    }
}