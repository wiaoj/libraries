using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.BloomFilter.Engine;
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
            await service.StopAsync(TestContext.Current.CancellationToken);
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

            await service.StartAsync(TestContext.Current.CancellationToken);

            // Act
            await service.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(filter.IsDirty);
            Assert.True(this._storage.Exists("shutdown-filter"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public async Task Should_ImmediatelyComplete_When_AutoSaveIntervalIsZeroOrNegative(int seconds) {
            // Arrange
            BloomFilterOptions options = new();
            options.Lifecycle.AutoSaveInterval = TimeSpan.FromSeconds(seconds);
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            using BloomFilterAutoSaveService service = new(
                this._registry,
                this._fakeTime,
                optionsWrapper,
                NullLogger<BloomFilterAutoSaveService>.Instance
            );

            // Act & Assert: starting service should immediately finish ExecuteAsync
            await service.StartAsync(TestContext.Current.CancellationToken);
            if(service.ExecuteTask != null) {
                await service.ExecuteTask;
                Assert.True(service.ExecuteTask.IsCompleted);
            }
        }

        [Fact]
        public async Task Should_ContinueSavingRemainingFilters_When_OneFilterFailsDuringAutoSave() {
            // Arrange
            BloomFilterOptions options = new();
            options.Lifecycle.AutoSaveInterval = TimeSpan.FromMinutes(1);
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            FaultyFilter faultyFilter = new("faulty-filter");
            faultyFilter.Add("faulty-item");
            this._registry.Register(faultyFilter);

            using InMemoryBloomFilter healthyFilter = CreateTestFilter("healthy-filter");
            healthyFilter.Add("healthy-item"u8);

            using BloomFilterAutoSaveService service = new(
                this._registry,
                this._fakeTime,
                optionsWrapper,
                NullLogger<BloomFilterAutoSaveService>.Instance
            );

            using CancellationTokenSource cts = new();
            await service.StartAsync(cts.Token);
            await Task.Yield();
            await Task.Delay(20, TestContext.Current.CancellationToken);

            // Act: Trigger tick
            this._fakeTime.Advance(TimeSpan.FromMinutes(1));

            // Spin until healthyFilter is saved
            bool healthySaved = SpinWait.SpinUntil(() => !healthyFilter.IsDirty, 2000);

            // Assert: healthy filter was saved despite faulty filter throwing
            Assert.True(healthySaved);
            Assert.True(this._storage.Exists("healthy-filter"));

            cts.Cancel();
            await service.StopAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Should_ContinueSavingRemainingFilters_When_OneFilterFailsDuringStopAsync() {
            // Arrange
            BloomFilterOptions options = new();
            options.Lifecycle.AutoSaveInterval = TimeSpan.FromHours(1);
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            FaultyFilter faultyFilter = new("faulty-shutdown-filter");
            faultyFilter.Add("faulty-item");
            this._registry.Register(faultyFilter);

            using InMemoryBloomFilter healthyFilter = CreateTestFilter("healthy-shutdown-filter");
            healthyFilter.Add("healthy-data"u8);

            using BloomFilterAutoSaveService service = new(
                this._registry,
                this._fakeTime,
                optionsWrapper,
                NullLogger<BloomFilterAutoSaveService>.Instance
            );

            await service.StartAsync(TestContext.Current.CancellationToken);

            // Act: StopAsync triggers final save
            await service.StopAsync(TestContext.Current.CancellationToken);

            // Assert: healthy filter must have saved despite faulty filter failure
            Assert.False(healthyFilter.IsDirty);
            Assert.True(this._storage.Exists("healthy-shutdown-filter"));
        }
    }

    private sealed class FaultyFilter(string name) : FakeBloomFilter(name) {
        public override ValueTask SaveAsync(CancellationToken cancellationToken = default) {
            throw new InvalidOperationException("Simulated save failure in faulty filter");
        }
    }
}