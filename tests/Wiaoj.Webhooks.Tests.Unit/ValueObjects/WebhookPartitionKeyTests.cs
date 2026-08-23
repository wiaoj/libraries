using System.Text.Json;

namespace Wiaoj.Webhooks.Tests.Unit.ValueObjects;

[Trait("Category", "Unit")]
[Trait("Feature", "ValueObjects")]
[Trait("Component", "PartitionKey")]
public sealed class WebhookPartitionKeyTests {

    [Fact]
    public void Constructor_SetsValue_WhenValid() {
        WebhookPartitionKey key = new("tenant_42");
        Assert.Equal("tenant_42", key.Value);
        Assert.Equal("tenant_42", key.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenValueIsNullOrWhiteSpace(string? invalid) {
        Assert.ThrowsAny<ArgumentException>(() => new WebhookPartitionKey(invalid!));
    }

    [Fact]
    public void Parse_And_TryParse_WorkAcrossStringAndSpans() {
        WebhookPartitionKey key1 = WebhookPartitionKey.Parse("order_100");
        WebhookPartitionKey key2 = WebhookPartitionKey.Parse("order_100".AsSpan());

        Assert.Equal("order_100", key1.Value);
        Assert.Equal(key1, key2);

        Assert.True(WebhookPartitionKey.TryParse("order_200", out WebhookPartitionKey try1));
        Assert.True(WebhookPartitionKey.TryParse("order_200".AsSpan(), out WebhookPartitionKey try2));
        Assert.Equal("order_200", try1.Value);
        Assert.Equal(try1, try2);

        Assert.False(WebhookPartitionKey.TryParse(null, out _));
        Assert.False(WebhookPartitionKey.TryParse("   ", out _));
    }

    [Fact]
    public void ImplicitConversions_WorkSeamlessly() {
        // String -> WebhookPartitionKey
        WebhookPartitionKey keyFromString = "customer-99";
        Assert.Equal("customer-99", keyFromString.Value);

        // WebhookEndpointId -> WebhookPartitionKey
        WebhookEndpointId endpointId = new("ep-customer-1");
        WebhookPartitionKey keyFromEndpoint = endpointId;
        Assert.Equal("ep-customer-1", keyFromEndpoint.Value);

        // WebhookPartitionKey -> String
        string stringVal = keyFromString;
        Assert.Equal("customer-99", stringVal);
    }

    [Fact]
    public void Comparer_SupportsZeroAllocationSpanLookup() {
        WebhookPartitionKey key = new("active_shard");
        Dictionary<WebhookPartitionKey, string> map = new(WebhookPartitionKey.Comparer) {
            [key] = "registered"
        };

        Dictionary<WebhookPartitionKey, string>.AlternateLookup<ReadOnlySpan<char>> lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        Assert.True(lookup.TryGetValue("active_shard".AsSpan(), out string? value));
        Assert.Equal("registered", value);
    }

    [Fact]
    public void SystemTextJson_SerializesAsFlatString() {
        WebhookPartitionKey key = new("json_partition");
        string json = JsonSerializer.Serialize(key);

        Assert.Equal("\"json_partition\"", json);

        WebhookPartitionKey deserialized = JsonSerializer.Deserialize<WebhookPartitionKey>(json);
        Assert.Equal(key, deserialized);
    }
}