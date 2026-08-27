using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Wiaoj.Webhooks.AspNetCore;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore.Discriminators;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "HeaderDiscriminator")]
public sealed class HeaderEventDiscriminatorExtractorTests {

    public sealed class TheTryExtractEventNameMethod {
        [Fact]
        public void TryExtractEventName_WhenSingleHeaderIsPresent_ExtractsValueSuccessfully() {
            HeaderEventDiscriminatorExtractor extractor = new("X-GitHub-Event");
            DefaultHttpContext context = new();
            context.Request.Headers["X-GitHub-Event"] = "push";

            bool result = extractor.TryExtractEventName(context, [], out string? eventName);

            Assert.True(result);
            Assert.Equal("push", eventName);
        }

        [Fact]
        public void TryExtractEventName_TrimsWhitespaceAroundHeaderValue() {
            HeaderEventDiscriminatorExtractor extractor = new("Webhook-Event");
            DefaultHttpContext context = new();
            context.Request.Headers["Webhook-Event"] = "  order.created   ";

            bool result = extractor.TryExtractEventName(context, [], out string? eventName);

            Assert.True(result);
            Assert.Equal("order.created", eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenHeaderIsMissing_ReturnsFalse() {
            HeaderEventDiscriminatorExtractor extractor = new("X-GitHub-Event");
            DefaultHttpContext context = new();

            bool result = extractor.TryExtractEventName(context, [], out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void TryExtractEventName_WhenHeaderIsEmptyOrWhitespace_ReturnsFalse(string emptyHeaderValue) {
            HeaderEventDiscriminatorExtractor extractor = new("X-GitHub-Event");
            DefaultHttpContext context = new();
            context.Request.Headers["X-GitHub-Event"] = emptyHeaderValue;

            bool result = extractor.TryExtractEventName(context, [], out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void TryExtractEventName_WhenMultipleHeadersPresent_PollutionDefense_ReturnsFalse() {
            HeaderEventDiscriminatorExtractor extractor = new("X-GitHub-Event");
            DefaultHttpContext context = new();
            context.Request.Headers["X-GitHub-Event"] = new StringValues(["push", "admin.action"]);

            bool result = extractor.TryExtractEventName(context, [], out string? eventName);

            Assert.False(result);
            Assert.Null(eventName);
        }

        [Fact]
        public void TryExtractEventName_Throws_WhenContextIsNull() {
            HeaderEventDiscriminatorExtractor extractor = new("X-GitHub-Event");

            Assert.ThrowsAny<ArgumentNullException>(() =>
                extractor.TryExtractEventName(null!, [], out _));
        }
    }

    public sealed class TheConstructor {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenHeaderNameIsNullOrWhiteSpace(string? invalidHeader) {
            Assert.ThrowsAny<ArgumentException>(() => new HeaderEventDiscriminatorExtractor(invalidHeader!));
        }
    }
}