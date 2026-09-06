using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.Metrics;
using Wiaoj.DistributedCounter;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.Diagnostics;

/// <summary>
/// Pins the emitted instrument names to the OpenTelemetry naming convention: lowercase, dot-separated
/// namespaces, snake_case within a segment, and no Prometheus-style ".total" suffix (exporters append
/// their own). These names are a public contract — dashboards and alerts break silently when they drift.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "Tracing")]
public sealed class ResilienceInstrumentNamingTests {

    private const string MeterName = "Wiaoj.Resilience";

    /// <summary>
    /// Drives one decision through the public API so the static metrics class is initialized and its
    /// instruments exist, then reports what the meter has published.
    /// </summary>
    private static async Task<IReadOnlyList<string>> PublishedInstrumentNamesAsync() {
        ServiceCollection services = new();
        services.AddDistributedCounter(c => c.UseInMemory());
        IDistributedCounterFactory counterFactory = services.BuildServiceProvider()
            .GetRequiredService<IDistributedCounterFactory>();

        ConsecutiveFailuresCircuitBreaker breaker = new(
            counterFactory,
            new CircuitBreakerOptions(),
            TimeProvider.System,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        await breaker.TryAcquireAsync("instrument-naming-probe", TestContext.Current.CancellationToken);

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

        Assert.Contains("circuit_breaker.decisions", observed);
        Assert.Contains("circuit_breaker.trips", observed);
        Assert.Contains("circuit_breaker.successes", observed);
        Assert.Contains("circuit_breaker.failures", observed);
        Assert.Contains("circuit_breaker.state", observed);
    }

    [Fact]
    public async Task Meter_PublishesNoInstrumentThatDeviatesFromTheConvention() {
        IReadOnlyList<string> observed = await PublishedInstrumentNamesAsync();

        Assert.NotEmpty(observed);

        foreach(string name in observed) {
            Assert.Equal(name.ToLowerInvariant(), name);
            Assert.StartsWith("circuit_breaker.", name);
            Assert.False(
                name.EndsWith(".total", StringComparison.Ordinal),
                $"'{name}' carries a Prometheus-style .total suffix; OTel exporters append their own.");
        }
    }
}
