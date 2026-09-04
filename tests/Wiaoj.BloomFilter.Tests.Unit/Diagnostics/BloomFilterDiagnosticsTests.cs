using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit.Diagnostics;

public class BloomFilterDiagnosticsTests : IDisposable {
    private readonly MeterListener _meterListener;
    private readonly List<(Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)> _measurements = new();
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public BloomFilterDiagnosticsTests() {
        this._meterListener = new MeterListener();
        this._meterListener.InstrumentPublished = (instrument, listener) => {
            if(instrument.Meter.Name == BloomFilterDiagnostics.MeterName) {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        this._meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            lock(this._measurements) {
                this._measurements.Add((instrument, value, tags.ToArray()));
            }
        });
        this._meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => {
            lock(this._measurements) {
                this._measurements.Add((instrument, value, tags.ToArray()));
            }
        });
        this._meterListener.Start();
    }

    public void Dispose() {
        this._meterListener.Dispose();
        GC.SuppressFinalize(this);
    }

    private InMemoryBloomFilter CreateFilter(string name, long capacity = 1_000, FakeBloomFilterStorage? storage = null) {
        FakeBloomFilterStorage effectiveStorage = storage ?? new FakeBloomFilterStorage();
        BloomFilterOptions options = new();
        BloomFilterContext context = new(
            Storage: effectiveStorage,
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );

        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse(name), capacity, 0.01);
        return new InMemoryBloomFilter(config, context);
    }

    private (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? GetMeasurement(string instrumentName, string filterName) {
        lock(this._measurements) {
            int index = this._measurements.FindIndex(m =>
                m.Instrument.Name == instrumentName &&
                m.Tags.Any(t => t.Key == BloomFilterDiagnostics.TagFilterName && Equals(t.Value, filterName)));
            return index >= 0 ? this._measurements[index] : null;
        }
    }

    [Fact]
    public void Should_RecordLookupAndHitCounters_When_ItemExists() {
        // Arrange
        string filterName = $"hit-test-{Guid.NewGuid():N}";
        using InMemoryBloomFilter filter = CreateFilter(filterName);
        byte[] item = Encoding.UTF8.GetBytes("test-existing-item");
        filter.Add(item);

        // Act
        bool exists = filter.Contains(item);

        // Assert
        Assert.True(exists);
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? lookupRecord = GetMeasurement(BloomFilterDiagnostics.LookupCounter.Name, filterName);
        Assert.NotNull(lookupRecord);
        Assert.Equal(1L, (long)lookupRecord.Value.Value);

        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? hitRecord = GetMeasurement(BloomFilterDiagnostics.HitCounter.Name, filterName);
        Assert.NotNull(hitRecord);
        Assert.Equal(1L, (long)hitRecord.Value.Value);
    }

    [Fact]
    public void Should_RecordLookupCounter_WithoutHitCounter_When_ItemDoesNotExist() {
        // Arrange
        string filterName = $"miss-test-{Guid.NewGuid():N}";
        using InMemoryBloomFilter filter = CreateFilter(filterName);
        byte[] missingItem = Encoding.UTF8.GetBytes("test-non-existing-item");

        // Act
        bool exists = filter.Contains(missingItem);

        // Assert
        Assert.False(exists);
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? lookupRecord = GetMeasurement(BloomFilterDiagnostics.LookupCounter.Name, filterName);
        Assert.NotNull(lookupRecord);
        Assert.Equal(1L, (long)lookupRecord.Value.Value);

        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? hitRecord = GetMeasurement(BloomFilterDiagnostics.HitCounter.Name, filterName);
        Assert.Null(hitRecord);
    }

    [Fact]
    public void Should_RecordAddCounter_When_ItemIsAdded() {
        // Arrange
        string filterName = $"add-test-{Guid.NewGuid():N}";
        using InMemoryBloomFilter filter = CreateFilter(filterName);
        byte[] item = Encoding.UTF8.GetBytes("item-to-add");

        // Act
        filter.Add(item);

        // Assert
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? addRecord = GetMeasurement(BloomFilterDiagnostics.AddCounter.Name, filterName);
        Assert.NotNull(addRecord);
        Assert.Equal(1L, (long)addRecord.Value.Value);
    }

    [Fact]
    public async Task Should_RecordSaveDurationAndBytesWritten_When_SaveAsyncCompletes() {
        // Arrange
        string filterName = $"save-test-{Guid.NewGuid():N}";
        FakeBloomFilterStorage storage = new();
        using InMemoryBloomFilter filter = CreateFilter(filterName, storage: storage);
        filter.Add(Encoding.UTF8.GetBytes("save-item"));

        // Act
        await filter.SaveAsync(TestContext.Current.CancellationToken);

        // Assert
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? durationRecord = GetMeasurement(BloomFilterDiagnostics.SaveDuration.Name, filterName);
        Assert.NotNull(durationRecord);
        Assert.True((double)durationRecord.Value.Value >= 0);

        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? bytesRecord = GetMeasurement(BloomFilterDiagnostics.BytesWrittenCounter.Name, filterName);
        Assert.NotNull(bytesRecord);
        Assert.True((long)bytesRecord.Value.Value > 0);
    }

    [Fact]
    public async Task Should_RecordReloadDuration_When_ReloadAsyncCompletes() {
        // Arrange
        string filterName = $"reload-test-{Guid.NewGuid():N}";
        FakeBloomFilterStorage storage = new();
        using InMemoryBloomFilter filter = CreateFilter(filterName, storage: storage);
        filter.Add(Encoding.UTF8.GetBytes("reload-item"));
        await filter.SaveAsync(TestContext.Current.CancellationToken);

        // Act
        await filter.ReloadAsync(TestContext.Current.CancellationToken);

        // Assert
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? durationRecord = GetMeasurement(BloomFilterDiagnostics.ReloadDuration.Name, filterName);
        Assert.NotNull(durationRecord);
        Assert.True((double)durationRecord.Value.Value >= 0);
    }

    [Fact]
    public async Task Should_RecordSeedingDuration_When_BloomFilterSeederCompletes() {
        // Arrange
        string filterName = $"seed-test-{Guid.NewGuid():N}";
        FakeBloomFilterStorage storage = new();
        InMemoryBloomFilter filter = CreateFilter(filterName, storage: storage);

        ServiceCollection services = new();
        services.AddKeyedSingleton<IPersistentBloomFilter>(filterName, filter);
        IServiceProvider sp = services.BuildServiceProvider();

        BloomFilterSeeder seeder = new(sp, NullLogger<BloomFilterSeeder>.Instance);

        static async IAsyncEnumerable<string> Items() {
            yield return "alpha";
            yield return "beta";
            yield return "gamma";
            await Task.CompletedTask;
        }

        // Act
        await seeder.SeedAsync(FilterName.Parse(filterName), Items(), TestContext.Current.CancellationToken);

        // Assert
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? seedingRecord = GetMeasurement(BloomFilterDiagnostics.SeedingDuration.Name, filterName);
        Assert.NotNull(seedingRecord);
        Assert.True((double)seedingRecord.Value.Value >= 0);
    }

    [Fact]
    public void Should_RecordScalableLayerSpawnCounter_When_ScaleUpOccurs() {
        // Arrange
        string filterName = $"scaleup-test-{Guid.NewGuid():N}";
        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse(filterName), 1_000, 0.01);
        BloomFilterContext context = new(
            Storage: new FakeBloomFilterStorage(),
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: new BloomFilterOptions(),
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );

        using ScalableBloomFilter scalable = new(config, context, GrowthRate.Double, Percentage.FromDouble(0.50));

        // Act - insert enough items (10_000) to trigger saturation and scale-up
        for(int i = 0; i < 10_000; i++) {
            scalable.Add(Encoding.UTF8.GetBytes($"scalable-key-{i}"));
        }

        // Assert
        (Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)? spawnRecord = GetMeasurement(BloomFilterDiagnostics.ScalableLayerSpawnCounter.Name, filterName);
        Assert.NotNull(spawnRecord);
        Assert.True((long)spawnRecord.Value.Value >= 1L);
    }
}
