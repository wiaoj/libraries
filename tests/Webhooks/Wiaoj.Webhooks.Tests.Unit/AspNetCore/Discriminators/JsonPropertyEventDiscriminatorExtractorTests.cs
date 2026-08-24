using System.Text;
using Microsoft.AspNetCore.Http;
using Wiaoj.Webhooks.AspNetCore;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore.Discriminators;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "JsonPropertyDiscriminator")]
public sealed class JsonPropertyEventDiscriminatorExtractorTests {

    public sealed class TheTryExtractEventNameMethod {
        [Fact]
        public void TryExtractEventName_WhenPropertyIsAtRoot_ExtractsSuccessfully() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"id":"evt_100","type":"payment_intent.succeeded","created":1700000000}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.True(result);
            Assert.Equal("payment_intent.succeeded", eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenCustomPropertyNameProvided_ExtractsCorrectly() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("event_name");
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"event_name":"customer.subscription.deleted","active":true}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.True(result);
            Assert.Equal("customer.subscription.deleted", eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenNestedObjectContainsSameKey_IgnoresNestedAndLooksAtRoot() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"data":{"type":"nested.should.be.ignored"},"type":"root.event.correct"}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.True(result);
            Assert.Equal("root.event.correct", eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenTargetPropertyDoesNotExist_ReturnsFalse() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"id":"evt_100","event":"order.created"}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenPropertyIsNotAString_ReturnsFalse() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"type":12345,"amount":100}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenJsonIsMalformed_ReturnsFalse_WithoutThrowing() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{ malformed json payload without quotes """);

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenBodyIsEmpty_ReturnsFalse() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");
            DefaultHttpContext context = new();

            bool result = extractor.TryExtractEventName(context, ReadOnlySpan<byte>.Empty, out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void TryExtractEventName_Throws_WhenContextIsNull() {
            JsonPropertyEventDiscriminatorExtractor extractor = new("type");

            Assert.ThrowsAny<ArgumentNullException>(() =>
                extractor.TryExtractEventName(null!, ReadOnlySpan<byte>.Empty, out _));
        }
    }

    public sealed class TheConstructor {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenPropertyNameIsNullOrWhiteSpace(string? invalidPropertyName) {
            Assert.ThrowsAny<ArgumentException>(() => new JsonPropertyEventDiscriminatorExtractor(invalidPropertyName!));
        }
    }
}