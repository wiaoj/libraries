using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Hosting;

public class BloomFilterAutoSaveServiceTests {
    private readonly FakeTimeProvider _fakeTime = new();
    private readonly BloomFilterRegistry _registry = new();
    private readonly InMemoryBloomFilterStorage _storage = new();
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    private InMemoryBloomFilter CreateTestFilter(string name) {
        BloomFilterOptions options = new();
        BloomFilterContext context = new(
            this._storage,
            new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            NullLogger.Instance,
            options,
            this._fakeTime,
            this._configFactory
        );

        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse(name), 1_000, 0.01);
        InMemoryBloomFilter filter = new(config, context);
        this._registry.Register(filter);
        return filter;
    }

    public sealed class ExecutionAndGracefulShutdown : BloomFilterAutoSaveServiceTests {
        [Fact]
        public async Task Should_SaveDirtyFilters_When_TimerTicks() {
            // Arrange
            BloomFilterOptions options = new();
            options.Lifecycle.AutoSaveInterval = TimeSpan.FromMinutes(5);
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            using InMemoryBloomFilter filter = CreateTestFilter("auto-save-filter");
            filter.Add("pending-data"u8);
            Assert.True(filter.IsDirty);

            using BloomFilterAutoSaveService service = new(
                this._registry,
                this._fakeTime,
                optionsWrapper,
                NullLogger<BloomFilterAutoSaveService>.Instance
            );

            using CancellationTokenSource cts = new();

            // Act: Start service in background
            Task executeTask = service.StartAsync(cts.Token);

            // Advance time to trigger timer tick
            this._fakeTime.Advance(TimeSpan.FromMinutes(5));

            // Wait a small delay for execution cycle
            await Task.Delay(50);

            // Assert
            Assert.False(filter.IsDirty);
            Assert.True(this._storage.Exists("auto-save-filter"));

            // Cleanup
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Should_PerformFinalSave_OnGracefulShutdown() {
            // Arrange
            BloomFilterOptions options = new();
            options.Lifecycle.AutoSaveInterval = TimeSpan.FromHours(1); // Long interval (won't tick)
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            using InMemoryBloomFilter filter = CreateTestFilter("shutdown-filter");
            filter.Add("shutdown-data"u8);
            Assert.True(filter.IsDirty);

            using BloomFilterAutoSaveService service = new(
                this._registry,
                this._fakeTime,
                optionsWrapper,
                NullLogger<BloomFilterAutoSaveService>.Instance
            );

            await service.StartAsync(CancellationToken.None);

            // Act: Request service stop
            await service.StopAsync(CancellationToken.None);

            // Assert: Filter must have been saved during shutdown
            Assert.False(filter.IsDirty);
            Assert.True(this._storage.Exists("shutdown-filter"));
        }
    }
}