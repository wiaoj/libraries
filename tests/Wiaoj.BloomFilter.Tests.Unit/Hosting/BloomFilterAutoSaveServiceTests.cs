using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Hosting;

public class BloomFilterAutoSaveServiceTests {
    private readonly FakeTimeProvider _fakeTime = new();
    private readonly BloomFilterRegistry _registry = new();
    private readonly FakeBloomFilterStorage _storage = new();
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

            // Act: Start background service and allow loop to reach timer.WaitForNextTickAsync
            Task executeTask = service.StartAsync(cts.Token);
            await Task.Yield();
            await Task.Delay(20, TestContext.Current.CancellationToken);

            // Advance time to trigger timer tick
            this._fakeTime.Advance(TimeSpan.FromMinutes(5));

            // Spin until the background loop completes save operation
            bool saved = SpinWait.SpinUntil(() => !filter.IsDirty, 2000);

            // Assert
            Assert.True(saved);
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
            options.Lifecycle.AutoSaveInterval = TimeSpan.FromHours(1);
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

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            Assert.False(filter.IsDirty);
            Assert.True(this._storage.Exists("shutdown-filter"));
        }
    }
}