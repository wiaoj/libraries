using System.Text;
using Microsoft.AspNetCore.Http;
using Wiaoj.Webhooks.AspNetCore;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore.Discriminators;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "CompositeDiscriminator")]
public sealed class CompositeEventDiscriminatorExtractorTests {

    public sealed class TheTryExtractEventNameMethod {
        [Fact]
        public void TryExtractEventName_PrefersHeader_WhenBothHeaderAndBodyArePresent() {
            CompositeEventDiscriminatorExtractor extractor = new(
                new HeaderEventDiscriminatorExtractor("X-GitHub-Event"),
                new JsonPropertyEventDiscriminatorExtractor("type"));

            DefaultHttpContext context = new();
            context.Request.Headers["X-GitHub-Event"] = "issues";
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"type":"push"}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.True(result);
            Assert.Equal("issues", eventName);
        }

        [Fact]
        public void TryExtractEventName_FallsBackToBody_WhenHeaderIsMissing() {
            CompositeEventDiscriminatorExtractor extractor = new(
                new HeaderEventDiscriminatorExtractor("X-GitHub-Event"),
                new JsonPropertyEventDiscriminatorExtractor("type"));

            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"type":"payment_intent.succeeded"}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.True(result);
            Assert.Equal("payment_intent.succeeded", eventName);
        }

        [Fact]
        public void TryExtractEventName_ReturnsFalse_WhenNoExtractorsMatch() {
            CompositeEventDiscriminatorExtractor extractor = new(
                new HeaderEventDiscriminatorExtractor("X-Custom-Event"),
                new JsonPropertyEventDiscriminatorExtractor("event"));

            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"unrelated":"data"}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void Default_Instance_EvaluatesStandardHeadersAndJsonRoots() {
            CompositeEventDiscriminatorExtractor extractor = CompositeEventDiscriminatorExtractor.Default;
            DefaultHttpContext context = new();
            byte[] rawBody = Encoding.UTF8.GetBytes("""{"type":"order.completed"}""");

            bool result = extractor.TryExtractEventName(context, rawBody, out string? eventName);

            Assert.True(result);
            Assert.Equal("order.completed", eventName);
        }

        [Fact]
        public void TryExtractEventName_Throws_WhenContextIsNull() {
            CompositeEventDiscriminatorExtractor extractor = CompositeEventDiscriminatorExtractor.Default;

            Assert.ThrowsAny<ArgumentNullException>(() =>
                extractor.TryExtractEventName(null!, [], out _));
        }
    }

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenExtractorsArrayIsNullOrEmpty() {
            Assert.ThrowsAny<ArgumentNullException>(() => new CompositeEventDiscriminatorExtractor(null!));
            Assert.ThrowsAny<ArgumentException>(() => new CompositeEventDiscriminatorExtractor([]));
        }
    }
}