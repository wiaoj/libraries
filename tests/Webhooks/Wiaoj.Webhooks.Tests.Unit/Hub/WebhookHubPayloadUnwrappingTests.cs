using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Tests.Unit.Hub;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "PayloadUnwrapping")]
public sealed class WebhookHubPayloadUnwrappingTests {

    public sealed record StripePaymentIntentDto(string Id, decimal Amount, string Currency);

    private static (DefaultHttpContext HttpContext, ServiceProvider ServiceProvider) CreateContext(string body) {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions()));
        services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddOptions<WebhookInboundOptions>();

        ServiceProvider sp = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() {
            RequestServices = sp
        };
        httpContext.Request.Path = "/api/webhooks/stripe";
        httpContext.Request.Method = "POST";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentLength = bodyBytes.Length;

        return (httpContext, sp);
    }

    [Fact]
    public async Task InvokeAsync_WhenPayloadPathConfigured_UnwrapsAndDeserializesDirectlyIntoDto() {
        // Arrange
        WebhookHubMetadata metadata = new() {
            RequireSignature = false,
            EventExtractor = new JsonPropertyEventDiscriminatorExtractor("type"),
            PayloadPath = "data.object"
        };

        StripePaymentIntentDto? capturedPayment = null;
        metadata.AddRegistration(new WebhookHubRegistration(
            "payment_intent.succeeded",
            typeof(StripePaymentIntentDto),
            (StripePaymentIntentDto payment) => {
                capturedPayment = payment;
                return Results.Ok();
            }));

        WebhookHubEndpointFilter filter = new(metadata);

        const string stripeEnvelope = """
        {
          "id": "evt_test_100",
          "type": "payment_intent.succeeded",
          "data": {
            "object": {
              "Id": "pi_998877",
              "Amount": 49.99,
              "Currency": "USD"
            }
          }
        }
        """;

        (DefaultHttpContext ctx, _) = CreateContext(stripeEnvelope);

        // Act
        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(ctx),
            static _ => ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        IStatusCodeHttpResult status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);

        Assert.NotNull(capturedPayment);
        Assert.Equal("pi_998877", capturedPayment.Id);
        Assert.Equal(49.99m, capturedPayment.Amount);
        Assert.Equal("USD", capturedPayment.Currency);
    }
}