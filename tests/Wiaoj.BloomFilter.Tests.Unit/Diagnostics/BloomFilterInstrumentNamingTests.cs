using System.Diagnostics.Metrics;
using Wiaoj.BloomFilter.Diagnostics;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Diagnostics;

/// <summary>
/// Pins span, attribute and instrument names to the OpenTelemetry naming convention: lowercase,
/// dot-separated namespaces, snake_case within a segment, and no Prometheus-style ".total" suffix
/// (exporters append their own). These names are a public contract — dashboards and alerts break
/// silently when they drift.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "BloomFilter")]
[Trait("Component", "Tracing")]
public sealed class BloomFilterInstrumentNamingTests {

    public static TheoryData<string> SpanNames => [
        BloomFilterDiagnostics.ActivitySave,
        BloomFilterDiagnostics.ActivityReload,
        BloomFilterDiagnostics.ActivitySeeding,
        BloomFilterDiagnostics.ActivityWarmUp,
        BloomFilterDiagnostics.ActivityScaleUp
    ];

    public static TheoryData<string> TagNames => [
        BloomFilterDiagnostics.TagFilterName,
        BloomFilterDiagnostics.TagSizeInBits,
        BloomFilterDiagnostics.TagPopCount,
        BloomFilterDiagnostics.TagChecksum,
        BloomFilterDiagnostics.TagBytesWritten,
        BloomFilterDiagnostics.TagItemsSeeded,
        BloomFilterDiagnostics.TagLayerIndex
    ];

    [Fact]
    public void SpanNames_MatchTheExpectedContract() {
        Assert.Equal("bloom_filter.save", BloomFilterDiagnostics.ActivitySave);
        Assert.Equal("bloom_filter.reload", BloomFilterDiagnostics.ActivityReload);
        Assert.Equal("bloom_filter.seed", BloomFilterDiagnostics.ActivitySeeding);
        Assert.Equal("bloom_filter.warm_up", BloomFilterDiagnostics.ActivityWarmUp);
        Assert.Equal("bloom_filter.scale_up", BloomFilterDiagnostics.ActivityScaleUp);
    }

    [Theory]
    [MemberData(nameof(SpanNames))]
    public void SpanName_IsLowercaseAndNamespaced(string name) {
        Assert.Equal(name.ToLowerInvariant(), name);
        Assert.StartsWith("bloom_filter.", name);
    }

    [Theory]
    [MemberData(nameof(TagNames))]
    public void TagName_IsLowercaseAndNamespaced(string name) {
        Assert.Equal(name.ToLowerInvariant(), name);
        Assert.StartsWith("bloom_filter.", name);
    }

    [Fact]
    public void Meter_PublishesNoInstrumentThatDeviatesFromTheConvention() {
        // Touch the static class so its instruments are created before the listener starts.
        Assert.NotNull(BloomFilterDiagnostics.LookupCounter);

        List<string> observed = [];

        using MeterListener listener = new() {
            InstrumentPublished = (instrument, _) => {
                if(instrument.Meter.Name == BloomFilterDiagnostics.MeterName) {
                    lock(observed) {
                        observed.Add(instrument.Name);
                    }
                }
            }
        };

        listener.Start();

        Assert.NotEmpty(observed);

        foreach(string name in observed) {
            Assert.Equal(name.ToLowerInvariant(), name);
            Assert.StartsWith("bloom_filter.", name);
            Assert.False(
                name.EndsWith(".total", StringComparison.Ordinal),
                $"'{name}' carries a Prometheus-style .total suffix; OTel exporters append their own.");
        }
    }

    [Fact]
    public void Meter_PublishesTheExpectedInstrumentNames() {
        Assert.NotNull(BloomFilterDiagnostics.LookupCounter);

        List<string> observed = [];

        using MeterListener listener = new() {
            InstrumentPublished = (instrument, _) => {
                if(instrument.Meter.Name == BloomFilterDiagnostics.MeterName) {
                    lock(observed) {
                        observed.Add(instrument.Name);
                    }
                }
            }
        };

        listener.Start();

        Assert.Contains("bloom_filter.lookups", observed);
        Assert.Contains("bloom_filter.hits", observed);
        Assert.Contains("bloom_filter.items_added", observed);
        Assert.Contains("bloom_filter.storage.bytes_written", observed);
        Assert.Contains("bloom_filter.scalable.layers_spawned", observed);
        Assert.Contains("bloom_filter.save.duration", observed);
        Assert.Contains("bloom_filter.reload.duration", observed);
        Assert.Contains("bloom_filter.seed.duration", observed);
    }
}
