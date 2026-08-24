using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "DeliveryContext")]
public sealed class WebhookDeliveryContextExtensionsTests {

    public sealed class TheGetHeadersMethod {

        [Fact]
        public void GetHeaders_WhenNoHeadersConfigured_ShouldBeTrulyImmutable_AndNotPolluteGlobalState() {
            // Arrange
            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext();
            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext();

            IReadOnlyDictionary<string, string> headers1 = context1.GetHeaders();

            // Act: Kötü niyetli veya dikkatsiz bir kodun downcast edip global singleton'ı bozma denemesi
            if(headers1 is IDictionary<string, string> mutableHeaders) {
                try {
                    mutableHeaders["X-Injected-Header"] = "Hacked";
                }
                catch(NotSupportedException) {
                    // Beklenen güvenli davranış: Eğer gerçekten read-only ise NotSupportedException fırlatır!
                }
            }

            // Assert
            IReadOnlyDictionary<string, string> headers2 = context2.GetHeaders();
            Assert.False(headers2.ContainsKey("X-Injected-Header"),
                "CRITICAL SECURITY FLAW: Global static dictionary was mutated across contexts!");
        }
    }
}