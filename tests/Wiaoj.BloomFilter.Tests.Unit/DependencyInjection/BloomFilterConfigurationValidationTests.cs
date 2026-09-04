using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.DependencyInjection;

public sealed class BloomFilterConfigurationValidationTests {
    private sealed record OrdersTag;

    [Fact]
    public void Should_ThrowValidationException_When_ExpectedItemsIsZeroOrNegative() {
        FilterDefinition def = new() {
            ExpectedItems = 0,
            ErrorRate = 0.01,
            Type = BloomFilterType.InMemory
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("zero-items"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.05)]
    [InlineData(1.5)]
    public void Should_ThrowValidationException_When_ErrorRateIsOutOfRange(double errorRate) {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = errorRate,
            Type = BloomFilterType.InMemory
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("bad-error-rate"));
    }

    [Theory]
    [InlineData(1)]  // Less than 2
    [InlineData(3)]  // Not power of 2
    [InlineData(5)]  // Not power of 2
    [InlineData(6)]  // Not power of 2
    [InlineData(10)] // Not power of 2
    public void Should_ThrowValidationException_When_ShardedTypeHasInvalidShardCount(int shardCount) {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Sharded,
            ShardCount = shardCount
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("bad-shards"));
    }

    [Fact]
    public void Should_PassValidation_When_ShardedTypeHasValidPowerOfTwoShardCount() {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Sharded,
            ShardCount = 8
        };

        // Should not throw
        def.Validate("valid-shards");
    }

    [Fact]
    public void Should_ThrowValidationException_When_RotatingFilterHasZeroOrNegativeWindowSize() {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Rotating,
            WindowSize = TimeSpan.Zero,
            ShardCount = 4
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("bad-window"));
    }

    [Fact]
    public void Should_ThrowValidationException_When_RotatingFilterHasZeroShardCount() {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Rotating,
            WindowSize = TimeSpan.FromHours(1),
            ShardCount = 0
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("zero-rotating-shards"));
    }

    [Theory]
    [InlineData(0.5)]  // Less than or equal to 1.0
    [InlineData(1.0)]
    public void Should_ThrowValidationException_When_ScalableFilterHasInvalidGrowthRate(double growthRate) {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Scalable,
            GrowthRate = growthRate,
            SaturationThreshold = 0.50
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("bad-growth"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Should_ThrowValidationException_When_ScalableFilterHasInvalidSaturationThreshold(double saturation) {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Scalable,
            GrowthRate = 2.0,
            SaturationThreshold = saturation
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate("bad-saturation"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_ThrowException_When_FilterNameIsNullOrEmptyOrWhitespace(string? filterName) {
        FilterDefinition def = new() {
            ExpectedItems = 10_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.InMemory
        };

        Assert.ThrowsAny<ArgumentException>(() => def.Validate(filterName!));
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_AutoSaveIntervalIsZeroOrNegative() {
        BloomFilterOptions options = new();
        options.Lifecycle.AutoSaveInterval = TimeSpan.Zero;

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());

        options.Lifecycle.AutoSaveInterval = TimeSpan.FromSeconds(-5);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Should_ThrowArgumentOutOfRangeException_When_ShardingThresholdBytesIsZeroOrNegative(long threshold) {
        BloomFilterOptions options = new();
        options.Lifecycle.ShardingThresholdBytes = threshold;

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Should_CascadeValidationToFilters_When_OptionsValidateIsCalled() {
        BloomFilterOptions options = new();
        options.Filters["invalid-filter"] = new FilterDefinition {
            ExpectedItems = -10,
            ErrorRate = 0.01,
            Type = BloomFilterType.InMemory
        };

        Assert.ThrowsAny<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Should_PassOptionsValidation_When_AllOptionsAndFiltersAreValid() {
        BloomFilterOptions options = new();
        options.Filters["valid-sharded"] = new FilterDefinition {
            ExpectedItems = 1_000_000,
            ErrorRate = 0.01,
            Type = BloomFilterType.Sharded,
            ShardCount = 8
        };
        options.Filters["valid-scalable"] = new FilterDefinition {
            ExpectedItems = 500_000,
            ErrorRate = 0.001,
            Type = BloomFilterType.Scalable,
            GrowthRate = 2.0,
            SaturationThreshold = 0.50
        };

        // Should not throw
        options.Validate();
    }
}
