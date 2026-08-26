using Wiaoj.Abstractions;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.Configuration;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "Configuration")]
public sealed class CircuitBreakerOptionsTests {

    [Fact]
    public void DeepClone_CreatesIndependentInstanceWithMatchingValues() {
        CircuitBreakerOptions original = new() {
            KeyPrefix = "custom:prefix:",
            FailureThreshold = 10,
            BreakDuration = TimeSpan.FromMinutes(2)
        };

        CircuitBreakerOptions clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.KeyPrefix, clone.KeyPrefix);
        Assert.Equal(original.FailureThreshold, clone.FailureThreshold);
        Assert.Equal(original.BreakDuration, clone.BreakDuration);
    }

    [Fact]
    public void Merge_CombinesPropertiesCorrectly() {
        CircuitBreakerOptions baseOptions = new() {
            KeyPrefix = "base:prefix:",
            FailureThreshold = 5,
            BreakDuration = TimeSpan.FromSeconds(30)
        };

        CircuitBreakerOptions overrideOptions = new() {
            KeyPrefix = "override:prefix:",
            FailureThreshold = 15,
            BreakDuration = TimeSpan.FromMinutes(5)
        };

        CircuitBreakerOptions merged = baseOptions.Merge(overrideOptions);

        Assert.Equal("override:prefix:", merged.KeyPrefix);
        Assert.Equal(15, merged.FailureThreshold);
        Assert.Equal(TimeSpan.FromMinutes(5), merged.BreakDuration);
    }

    [Fact]
    public void SamplingOptions_DeepCloneAndMerge_WorkCorrectly() {
        SamplingWindowCircuitBreakerOptions original = new() {
            FailureRateThreshold = 0.75,
            MinimumThroughput = 20,
            PermittedNumberOfCallsInHalfOpenState = 5,
            SamplingWindow = TimeSpan.FromSeconds(45),
            BreakDuration = TimeSpan.FromMinutes(3)
        };

        SamplingWindowCircuitBreakerOptions clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(0.75, clone.FailureRateThreshold);
        Assert.Equal(20, clone.MinimumThroughput);
        Assert.Equal(5, clone.PermittedNumberOfCallsInHalfOpenState);

        SamplingWindowCircuitBreakerOptions overrideOpt = new() {
            FailureRateThreshold = 0.3
        };

        SamplingWindowCircuitBreakerOptions merged = original.Merge(overrideOpt);
        Assert.Equal(0.3, merged.FailureRateThreshold);
    }
}