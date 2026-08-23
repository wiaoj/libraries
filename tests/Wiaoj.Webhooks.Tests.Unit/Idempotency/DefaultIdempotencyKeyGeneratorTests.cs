using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Idempotency;

[Trait("Category", "Unit")]
[Trait("Feature", "Idempotency")]
[Trait("Component", "KeyGenerator")]
public sealed class DefaultIdempotencyKeyGeneratorTests {
    private readonly DefaultIdempotencyKeyGenerator _generator = new();

    public sealed class TheGenerateKeyMethodWithContext {
        [Fact]
        public void GenerateKey_ProducesDeterministicKey_ForIdenticalContexts() {
            // Arrange
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId("customer-100"));
            const string payloadJson = """{"orderId":"ORD-100","amount":42.50}""";

            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(
                endpoint: endpoint,
                serializedPayload: payloadJson);

            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(
                endpoint: endpoint,
                serializedPayload: payloadJson);

            // Act
            DefaultIdempotencyKeyGenerator generator = new();
            IdempotencyKey key1 = generator.GenerateKey(context1);
            IdempotencyKey key2 = generator.GenerateKey(context2);

            // Assert
            Assert.NotEmpty(key1.Value);
            Assert.Equal(key1, key2);
            Assert.StartsWith($"idemp:{endpoint.Id.Value}:{context1.EventType}:", key1.Value);
        }

        [Fact]
        public void GenerateKey_ProducesDifferentKeys_WhenPayloadDiffers() {
            // Arrange
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(
                endpoint: endpoint,
                serializedPayload: """{"orderId":"ORD-100"}""");

            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(
                endpoint: endpoint,
                serializedPayload: """{"orderId":"ORD-200"}""");

            // Act
            DefaultIdempotencyKeyGenerator generator = new();
            IdempotencyKey key1 = generator.GenerateKey(context1);
            IdempotencyKey key2 = generator.GenerateKey(context2);

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public void GenerateKey_Throws_WhenContextIsNull() {
            DefaultIdempotencyKeyGenerator generator = new();
            Assert.ThrowsAny<ArgumentException>(() => generator.GenerateKey(null!));
        }
    }

    public sealed class TheGenerateKeyMethodWithExplicitParameters {
        [Fact]
        public void GenerateKey_ProducesDeterministicKey_ForSameInputs() {
            // Arrange
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("tenant-1");
            const string eventType = "payment.succeeded";
            const string payload = """{"paymentId":"pay_123"}""";

            // Act
            DefaultIdempotencyKeyGenerator generator = new();
            IdempotencyKey key1 = generator.GenerateKey(endpointId, eventType, payload);
            IdempotencyKey key2 = generator.GenerateKey(endpointId, eventType, payload);

            // Assert
            Assert.Equal(key1, key2);
            Assert.StartsWith("idemp:tenant-1:payment.succeeded:", key1.Value);
        }

        [Fact]
        public void GenerateKey_ProducesDifferentKeys_ForDifferentEndpoints() {
            // Arrange
            WebhookEndpointId endpoint1 = WebhookTestFactory.CreateEndpointId("endpoint-1");
            WebhookEndpointId endpoint2 = WebhookTestFactory.CreateEndpointId("endpoint-2");
            const string eventType = "order.created";
            const string payload = """{"id":1}""";

            // Act
            DefaultIdempotencyKeyGenerator generator = new();
            IdempotencyKey key1 = generator.GenerateKey(endpoint1, eventType, payload);
            IdempotencyKey key2 = generator.GenerateKey(endpoint2, eventType, payload);

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GenerateKey_Throws_WhenEventTypeIsNullOrWhiteSpace(string? invalidEventType) {
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
            DefaultIdempotencyKeyGenerator generator = new();

            Assert.ThrowsAny<ArgumentException>(() =>
                generator.GenerateKey(endpointId, invalidEventType!, "{}"));
        }

        [Fact]
        public void GenerateKey_Throws_WhenSerializedPayloadIsNull() {
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
            DefaultIdempotencyKeyGenerator generator = new();

            Assert.ThrowsAny<ArgumentException>(() =>
                generator.GenerateKey(endpointId, "order.created", null!));
        }
    }
}