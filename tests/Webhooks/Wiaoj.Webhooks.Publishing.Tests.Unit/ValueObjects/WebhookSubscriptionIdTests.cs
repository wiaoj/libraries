using System.Text.Json;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.ValueObjects;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "ValueObject")]
public sealed class WebhookSubscriptionIdTests {

    [Fact]
    public void NewId_GeneratesPrefixedTimeOrderedUuidV7() {
        WebhookSubscriptionId id1 = WebhookSubscriptionId.NewId();
        WebhookSubscriptionId id2 = WebhookSubscriptionId.NewId();

        Assert.StartsWith("sub_", id1.Value);
        Assert.StartsWith("sub_", id2.Value);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Parse_And_TryParse_WorkCorrectly() {
        const string raw = "sub_01918a56b3a27289b00e395f6170d1ab";

        WebhookSubscriptionId parsed1 = WebhookSubscriptionId.Parse(raw);
        WebhookSubscriptionId parsed2 = WebhookSubscriptionId.Parse(raw.AsSpan());

        Assert.Equal(raw, parsed1.Value);
        Assert.Equal(parsed1, parsed2);

        Assert.True(WebhookSubscriptionId.TryParse(raw, out WebhookSubscriptionId try1));
        Assert.True(WebhookSubscriptionId.TryParse(raw.AsSpan(), out WebhookSubscriptionId try2));
        Assert.Equal(raw, try1.Value);
        Assert.Equal(raw, try2.Value);

        Assert.False(WebhookSubscriptionId.TryParse(null, out _));
        Assert.False(WebhookSubscriptionId.TryParse("   ", out _));
    }

    [Fact]
    public void Equality_ComparesByValue() {
        WebhookSubscriptionId id1 = new("sub_123");
        WebhookSubscriptionId id2 = new("sub_123");
        WebhookSubscriptionId id3 = new("sub_456");

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void JsonSerialization_SerializesAsFlatString() {
        WebhookSubscriptionId id = new("sub_json_test");
        string json = JsonSerializer.Serialize(id);

        Assert.Equal("\"sub_json_test\"", json);

        WebhookSubscriptionId deserialized = JsonSerializer.Deserialize<WebhookSubscriptionId>(json);
        Assert.Equal(id, deserialized);
    }
}