using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting.DependencyInjection;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Diagnostics;

/// <summary>
/// Pins the emitted instrument names to the OpenTelemetry naming convention: lowercase, dot-separated
/// namespaces, snake_case within a segment, and no Prometheus-style ".total" suffix (exporters append
/// their own). These names are a public contract — dashboards and alerts break silently when they drift.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "RateLimiting")]
[Trait("Component", "Tracing")]
public sealed class RateLimitingInstrumentNamingTests {

    private const string MeterName = "Wiaoj.RateLimiting";

    /// <summary>
    /// Drives one acquire through the public API so the static metrics class is initialized and its
    /// instruments exist, then reports what the meter has published.
    /// </summary>
    private static async Task<IReadOnlyList<string>> PublishedInstrumentNamesAsync() {
        ServiceCollection services = new();
        services.AddDistributedCounter(c => c.UseInMemory());
        services.AddWiaojRateLimiting(limiter =>
            limiter.UseDefaultPolicy(policy =>
                policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(5))));

        IRateLimiter limiter = services.BuildServiceProvider().GetRequiredService<IRateLimiter>();
        await limiter.TryAcquireAsync("instrument-naming-probe", 1, TestContext.Current.CancellationToken);

        List<string> observed = [];

        using MeterListener listener = new() {
            InstrumentPublished = (instrument, _) => {
                if(instrument.Meter.Name == MeterName) {
                    lock(observed) {
                        observed.Add(instrument.Name);
                    }
                }
            }
        };

        listener.Start();
        return observed;
    }

    [Fact]
    public async Task Meter_PublishesTheExpectedInstrumentNames() {
        IReadOnlyList<string> observed = await PublishedInstrumentNamesAsync();

        Assert.Contains("rate_limit.decisions", observed);
        Assert.Contains("rate_limit.cost.consumed", observed);
        Assert.Contains("rate_limit.queue.wait_duration", observed);
    }

    [Fact]
    public async Task Meter_PublishesNoInstrumentThatDeviatesFromTheConvention() {
        IReadOnlyList<string> observed = await PublishedInstrumentNamesAsync();

        Assert.NotEmpty(observed);

        foreach(string name in observed) {
            Assert.Equal(name.ToLowerInvariant(), name);
            Assert.StartsWith("rate_limit.", name);
            Assert.False(
                name.EndsWith(".total", StringComparison.Ordinal),
                $"'{name}' carries a Prometheus-style .total suffix; OTel exporters append their own.");
        }
    }
}
