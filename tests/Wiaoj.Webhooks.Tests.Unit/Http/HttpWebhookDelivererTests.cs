using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Http;

[Trait("Category", "Unit")]
[Trait("Component", "HttpDelivery")]
public sealed class HttpWebhookDelivererTests {

    private static HttpWebhookDeliverer CreateDeliverer(HttpMessageHandler handler,
                                                        WebhookSecurityOptions? securityOptions = null) {
        HttpWebhookSender sender = new(new HttpClient(handler), NullLogger<HttpWebhookSender>.Instance);
        return new HttpWebhookDeliverer(
            sender,
            Options.Create(securityOptions ?? new WebhookSecurityOptions()),
            new FakeTimeProvider(),
            NullLogger<HttpWebhookDeliverer>.Instance);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR & ARGUMENT VALIDATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenSenderIsNull() {
            IOptions<WebhookSecurityOptions> options = Microsoft.Extensions.Options.Options.Create(new WebhookSecurityOptions());
            NullLogger<HttpWebhookDeliverer> logger = NullLogger<HttpWebhookDeliverer>.Instance;

            Assert.ThrowsAny<ArgumentException>(() =>
                new HttpWebhookDeliverer(null!, options, TimeProvider.System, logger));
        }

        [Fact]
        public void Constructor_Throws_WhenSecurityOptionsIsNull() {
            HttpWebhookSender sender = new(new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK)), NullLogger<HttpWebhookSender>.Instance);
            NullLogger<HttpWebhookDeliverer> logger = NullLogger<HttpWebhookDeliverer>.Instance;

            Assert.ThrowsAny<ArgumentException>(() =>
                new HttpWebhookDeliverer(sender, null!, TimeProvider.System, logger));
        }

        [Fact]
        public void Constructor_Throws_WhenTimeProviderIsNull() {
            HttpWebhookSender sender = new(new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK)), NullLogger<HttpWebhookSender>.Instance);
            IOptions<WebhookSecurityOptions> options = Microsoft.Extensions.Options.Options.Create(new WebhookSecurityOptions());
            NullLogger<HttpWebhookDeliverer> logger = NullLogger<HttpWebhookDeliverer>.Instance;

            Assert.ThrowsAny<ArgumentException>(() =>
                new HttpWebhookDeliverer(sender, options, null!, logger));
        }

        [Fact]
        public void Constructor_Throws_WhenLoggerIsNull() {
            HttpWebhookSender sender = new(new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK)), NullLogger<HttpWebhookSender>.Instance);
            IOptions<WebhookSecurityOptions> options = Microsoft.Extensions.Options.Options.Create(new WebhookSecurityOptions());

            Assert.ThrowsAny<ArgumentException>(() =>
                new HttpWebhookDeliverer(sender, options, TimeProvider.System, null!));
        }

        [Fact]
        public async Task DeliverAsync_Throws_WhenContextIsNull() {
            HttpWebhookDeliverer deliverer = WebhookTestFactory.CreateDeliverer();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                deliverer.DeliverAsync(null!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. SUCCESSFUL 2XX DELIVERIES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenTargetRespondsWith2xx {
        [Theory]
        [InlineData(HttpStatusCode.OK, "{\"status\":\"ok\"}")]
        [InlineData(HttpStatusCode.Created, "{\"id\":\"123\"}")]
        [InlineData(HttpStatusCode.Accepted, "{\"status\":\"queued\"}")]
        [InlineData(HttpStatusCode.NoContent, "")]
        public async Task DeliverAsync_ReturnsDeliveredResult_ForVarious2xxStatusCodes(HttpStatusCode statusCode, string responseBody) {
            FakeHttpMessageHandler handler = new(statusCode, responseBody: responseBody);
            HttpWebhookDeliverer deliverer = CreateDeliverer(handler);

            WebhookDeliveryResult result = await deliverer.DeliverAsync(WebhookTestFactory.CreateContext());

            WebhookDeliveryResult.Delivered delivered = Assert.IsType<WebhookDeliveryResult.Delivered>(result);
            Assert.True(delivered.IsSuccess);
            Assert.Equal((int)statusCode, delivered.StatusCode);
            Assert.Equal(responseBody, delivered.ResponseBody);
        }

        [Fact]
        public async Task DeliverAsync_SendsContextSerializedPayload_ToTargetUrl() {
            FakeHttpMessageHandler handler = new(HttpStatusCode.OK);
            HttpWebhookDeliverer deliverer = CreateDeliverer(handler);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await deliverer.DeliverAsync(context);

            Assert.Equal(context.TargetUrl, handler.LastRequest?.RequestUri);
            Assert.Equal(context.SerializedPayload, handler.LastRequestBody);
            Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        }

        [Fact]
        public async Task DeliverAsync_IncludesCustomHeaders_FromContext() {
            FakeHttpMessageHandler handler = new(HttpStatusCode.OK);
            HttpWebhookDeliverer deliverer = CreateDeliverer(handler);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            context.SetHeader("X-Signature", "sig_v1_12345");
            context.SetHeader("X-Event-Name", "order.created");

            await deliverer.DeliverAsync(context);

            Assert.NotNull(handler.LastRequest);
            Assert.True(handler.LastRequest.Headers.Contains("X-Signature"));
            Assert.True(handler.LastRequest.Headers.Contains("X-Event-Name"));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. TRANSIENT FAILURES (5xx, 429, 408) -> Must Produce TransientFailure
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenTargetRespondsWithTransientError {
        [Theory]
        [InlineData(HttpStatusCode.RequestTimeout)]       // 408
        [InlineData(HttpStatusCode.TooManyRequests)]      // 429
        [InlineData(HttpStatusCode.InternalServerError)]  // 500
        [InlineData(HttpStatusCode.BadGateway)]           // 502
        [InlineData(HttpStatusCode.ServiceUnavailable)]   // 503
        [InlineData(HttpStatusCode.GatewayTimeout)]       // 504
        public async Task DeliverAsync_ReturnsTransientFailure_ForTransientStatusCodes(HttpStatusCode statusCode) {
            FakeHttpMessageHandler handler = new(statusCode);
            HttpWebhookDeliverer deliverer = CreateDeliverer(handler);

            WebhookDeliveryResult result = await deliverer.DeliverAsync(WebhookTestFactory.CreateContext());

            WebhookDeliveryResult.TransientFailure failure = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
            Assert.False(failure.IsSuccess);
            Assert.Equal((int)statusCode, failure.StatusCode);
            Assert.Contains(((int)statusCode).ToString(), failure.ErrorMessage);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. PERMANENT FAILURES (400, 401, 403, 404) -> Must Produce PermanentFailure
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenTargetRespondsWithPermanentError {
        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]    // 400
        [InlineData(HttpStatusCode.Unauthorized)]  // 401
        [InlineData(HttpStatusCode.Forbidden)]     // 403
        [InlineData(HttpStatusCode.NotFound)]      // 404
        [InlineData(HttpStatusCode.Gone)]          // 410
        public async Task DeliverAsync_ReturnsPermanentFailure_ForNonTransientClientErrors(HttpStatusCode statusCode) {
            FakeHttpMessageHandler handler = new(statusCode);
            HttpWebhookDeliverer deliverer = CreateDeliverer(handler);

            WebhookDeliveryResult result = await deliverer.DeliverAsync(WebhookTestFactory.CreateContext());

            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.False(failure.IsSuccess);
            Assert.Equal((int)statusCode, failure.StatusCode);
            Assert.Equal(PermanentFailureReason.DestinationRejected, failure.Reason);
            Assert.Contains(((int)statusCode).ToString(), failure.ErrorMessage);
        }
    }
}