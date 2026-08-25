using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "PayloadPathSingleEndpoint")]
public sealed class WebhookReceiverEndpointFilterPayloadPathTests {

    public sealed class ThePayloadPathUnwrapping {

        private static (DefaultHttpContext HttpContext, ServiceProvider ServiceProvider) CreateContext(
            string body,
            Action<ServiceCollection>? configureServices = null) {

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions()));
            services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            services.AddOptions<WebhookInboundOptions>();

            configureServices?.Invoke(services);

            ServiceProvider sp = services.BuildServiceProvider();

            DefaultHttpContext httpContext = new() {
                RequestServices = sp
            };
            httpContext.Request.Path = "/api/webhooks/orders";
            httpContext.Request.Method = "POST";

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bodyBytes);
            httpContext.Request.ContentLength = bodyBytes.Length;

            return (httpContext, sp);
        }

        [Fact]
        public async Task InvokeAsync_WhenPayloadPathConfigured_UnwrapsSubtreeDirectlyIntoEventModel() {
            // Arrange
            OrderCreatedWebhookEvent? capturedEvent = null;

            Delegate handler = (OrderCreatedWebhookEvent e) => {
                capturedEvent = e;
                return Results.Ok();
            };

            WebhookReceiverEndpointMetadata metadata = new() {
                RequireSignature = false,
                PayloadPath = "data.object"
            };

            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, handler);

            const string wrappedEnvelope = """
            {
              "id": "evt_envelope_1",
              "data": {
                "object": {
                  "OrderId": "ORD-ENVELOPE-99",
                  "Amount": 150.75
                }
              }
            }
            """;

            (DefaultHttpContext httpContext, _) = CreateContext(wrappedEnvelope);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            Assert.NotNull(capturedEvent);
            Assert.Equal("ORD-ENVELOPE-99", capturedEvent.OrderId);
            Assert.Equal(150.75m, capturedEvent.Amount);
        }

        [Fact]
        public async Task InvokeAsync_WhenPayloadPathDoesNotExistInBody_Returns400BadRequest() {
            // Arrange
            WebhookReceiverEndpointMetadata metadata = new() {
                RequireSignature = false,
                PayloadPath = "data.non_existent_key"
            };

            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, static () => Results.Ok());

            const string wrappedEnvelope = """
            {
              "id": "evt_envelope_1",
              "data": {
                "object": {
                  "OrderId": "ORD-99",
                  "Amount": 10.00
                }
              }
            }
            """;

            (DefaultHttpContext httpContext, _) = CreateContext(wrappedEnvelope);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }
    }
}