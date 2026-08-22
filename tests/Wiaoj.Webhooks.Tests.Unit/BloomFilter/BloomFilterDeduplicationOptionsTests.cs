using Wiaoj.Webhooks.BloomFilter;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.BloomFilter;

[Trait("Category", "Unit")]
[Trait("Feature", "Deduplication")]
[Trait("Component", "BloomFilter")]
public sealed class BloomFilterDeduplicationOptionsTests {

    [Fact]
    public void Defaults_AreInitializedCorrectly() {
        BloomFilterDeduplicationOptions options = new();

        Assert.Equal(BloomFilterDeduplicationOptions.DefaultCapacity, options.Capacity);
        Assert.Equal(BloomFilterDeduplicationOptions.DefaultErrorRate, options.ErrorRate);
        Assert.NotNull(options.KeySelector);
    }

    [Fact]
    public void DefaultKeySelector_GeneratesConsistentKey() {
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        string key1 = BloomFilterDeduplicationOptions.DefaultKeySelector(context);
        string key2 = BloomFilterDeduplicationOptions.DefaultKeySelector(context);

        Assert.NotEmpty(key1);
        Assert.Equal(key1, key2);
        Assert.Contains(context.Endpoint.Id.Value, key1);
    }

    [Fact]
    public void CustomKeySelector_IsRespected() {
        BloomFilterDeduplicationOptions options = new() {
            KeySelector = ctx => $"custom:{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload.Length}"
        };

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        string key = options.KeySelector(context);

        Assert.StartsWith("custom:acme-1:", key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_Throws_WhenCapacityIsLessThanOne(long invalidCapacity) {
        BloomFilterDeduplicationOptions options = new() { Capacity = invalidCapacity };

        // Preca.ThrowIfLessThan -> PrecaArgumentOutOfRangeException fırlatır
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.01)]
    [InlineData(1.05)]
    public void Validate_Throws_WhenErrorRateIsOutOfRange(double invalidErrorRate) {
        BloomFilterDeduplicationOptions options = new() { ErrorRate = invalidErrorRate };

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Validate_Throws_WhenKeySelectorIsNull() {
        BloomFilterDeduplicationOptions options = new() { KeySelector = null! };
         
        Assert.ThrowsAny<ArgumentNullException>(() => options.Validate());
    }

    [Fact]
    public void Validate_Passes_WhenValuesAreValid() {
        BloomFilterDeduplicationOptions options = new() {
            Capacity = 500_000,
            ErrorRate = 0.005
        };

        Exception? exception = Record.Exception(() => options.Validate());
        Assert.Null(exception);
    }
}