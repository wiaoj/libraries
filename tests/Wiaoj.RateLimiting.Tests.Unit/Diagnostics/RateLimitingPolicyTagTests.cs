using System.Diagnostics.Metrics;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Diagnostics;

/// <summary>
/// An algorithm built with a policy name records its decisions under that name, so two policies on one meter can be
/// told apart. They were all recorded as <c>Default</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "RateLimiting")]
[Trait("Component", "Metrics")]
public sealed class RateLimitingPolicyTagTests {

    public static TheoryData<string> Algorithms => ["fixed", "sliding", "gcra"];

    private static IRateLimitAlgorithm Build(string algorithm, string policy) {
        FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        DistributedCounterTestContext context = new(time);
        return algorithm switch {
            "fixed" => new FixedWindowRateLimiter(context.Factory, policy, 1, TimeSpan.FromMinutes(1)),
            "sliding" => new SlidingWindowRateLimiter(context.Factory, policy, 1, TimeSpan.FromMinutes(1), time),
            _ => new GcraRateLimiter(context.Factory, policy, 1, TimeSpan.FromMinutes(1), time)
        };
    }

    [Theory]
    [MemberData(nameof(Algorithms))]
    public async Task EveryDecision_CarriesThePolicyTheAlgorithmWasBuiltWith(string algorithm) {
        string policy = $"policy-tag-{algorithm}-{Guid.NewGuid():N}";
        IRateLimitAlgorithm limiter = Build(algorithm, policy);
        List<(string Decision, string? Policy)> seen = [];
        HashSet<string> keys = [];

        using MeterListener listener = new() {
            InstrumentPublished = (instrument, l) => {
                if(instrument.Meter.Name == "Wiaoj.RateLimiting" && instrument.Name == "rate_limit.decisions") {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => {
            string? tagged = null;
            string? decision = null;
            foreach(KeyValuePair<string, object?> tag in tags) {
                if(tag.Key == "rate_limit.policy") { tagged = tag.Value as string; }
                if(tag.Key == "rate_limit.decision") { decision = tag.Value as string; }
            }
            if(tagged == policy) {
                lock(seen) { seen.Add((decision!, tagged)); }
            }
        });
        listener.Start();

        CancellationToken ct = TestContext.Current.CancellationToken;
        await limiter.TryAcquireAsync("k", 1, ct);
        await limiter.TryAcquireAsync("k", 1, ct);

        lock(seen) {
            Assert.Contains(("allowed", policy), seen);
            Assert.Contains(("denied", policy), seen);
            Assert.Equal(2, seen.Count);
        }
    }
}
